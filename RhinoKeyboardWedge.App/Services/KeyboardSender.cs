using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using RhinoKeyboardWedge.App.Configuration;

namespace RhinoKeyboardWedge.App.Services;

internal interface IKeyboardSender
{
    void SendText(string text, PostSendAction action);
}

internal sealed class KeyboardSender : IKeyboardSender
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const ushort VkReturn = 0x0D;
    private const ushort VkTab = 0x09;

    public void SendText(string text, PostSendAction action)
    {
        try
        {
            SendWithSendInput(text, action);
        }
        catch (Exception sendInputException)
        {
            try
            {
                SendWithSendKeys(text, action);
            }
            catch (Exception sendKeysException)
            {
                throw new InvalidOperationException(
                    $"No se pudo enviar la entrada de teclado. SendInput: {sendInputException.Message}. SendKeys: {sendKeysException.Message}",
                    sendKeysException);
            }
        }
    }

    private static void SendWithSendInput(string text, PostSendAction action)
    {
        var inputs = new List<Input>();

        foreach (var character in text)
        {
            AddCharacterInputs(inputs, character);
        }

        switch (action)
        {
            case PostSendAction.Enter:
                AddVirtualKeyInputs(inputs, VkReturn);
                break;
            case PostSendAction.Tab:
                AddVirtualKeyInputs(inputs, VkTab);
                break;
        }

        SendInputBatch(inputs);
    }

    private static void AddCharacterInputs(List<Input> inputs, char character)
    {
        if (character is >= '0' and <= '9')
        {
            AddVirtualKeyInputs(inputs, character);
            return;
        }

        inputs.Add(CreateUnicodeInput(character, keyUp: false));
        inputs.Add(CreateUnicodeInput(character, keyUp: true));
    }

    private static void AddVirtualKeyInputs(List<Input> inputs, int virtualKey)
    {
        inputs.Add(CreateVirtualKeyInput((ushort)virtualKey, keyUp: false));
        inputs.Add(CreateVirtualKeyInput((ushort)virtualKey, keyUp: true));
    }

    private static void SendInputBatch(IReadOnlyCollection<Input> inputs)
    {
        if (inputs.Count == 0)
        {
            return;
        }

        var inputArray = inputs.ToArray();
        var sent = SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf(typeof(Input)));
        if (sent != inputArray.Length)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"SendInput devolvio {sent} de {inputArray.Length} eventos.");
        }
    }

    private static void SendWithSendKeys(string text, PostSendAction action)
    {
        Exception? threadException = null;
        var sequence = BuildSendKeysSequence(text, action);

        var thread = new Thread(() =>
        {
            try
            {
                SendKeys.SendWait(sequence);
            }
            catch (Exception exception)
            {
                threadException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }
    }

    private static string BuildSendKeysSequence(string text, PostSendAction action)
    {
        var builder = new StringBuilder();
        foreach (var character in text)
        {
            builder.Append(EscapeSendKeysCharacter(character));
        }

        switch (action)
        {
            case PostSendAction.Enter:
                builder.Append("{ENTER}");
                break;
            case PostSendAction.Tab:
                builder.Append("{TAB}");
                break;
        }

        return builder.ToString();
    }

    private static string EscapeSendKeysCharacter(char character)
    {
        return character switch
        {
            '+' => "{+}",
            '^' => "{^}",
            '%' => "{%}",
            '~' => "{~}",
            '(' => "{(}",
            ')' => "{)}",
            '[' => "{[}",
            ']' => "{]}",
            '{' => "{{}",
            '}' => "{}}",
            _ => character.ToString()
        };
    }

    private static Input CreateUnicodeInput(char character, bool keyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = 0,
                    Scan = character,
                    Flags = KeyEventFUnicode | (keyUp ? KeyEventFKeyUp : 0)
                }
            }
        };
    }

    private static Input CreateVirtualKeyInput(ushort virtualKey, bool keyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Scan = 0,
                    Flags = keyUp ? KeyEventFKeyUp : 0
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamL;
        public ushort ParamH;
    }
}
