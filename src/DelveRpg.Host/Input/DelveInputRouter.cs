using System.Text;
using DelveRpg.Kit.Session;
using Rusty.Engine;

namespace DelveRpg.Host.Input;

/// <summary>
/// Translates Engine input events into one tick of semantic <see cref="RunInput"/>.
/// Held intents keep derived state that every <c>Clear</c> fact releases.
/// </summary>
public sealed class DelveInputRouter
{
    private const float DigitalThreshold = 0.5f;
    private const float LookSensitivity = 0.12f;

    private static readonly byte[] MoveForward = "move.forward"u8.ToArray();
    private static readonly byte[] MoveBack = "move.back"u8.ToArray();
    private static readonly byte[] MoveLeft = "move.left"u8.ToArray();
    private static readonly byte[] MoveRight = "move.right"u8.ToArray();
    private static readonly byte[] Attack = "attack"u8.ToArray();
    private static readonly byte[] Use = "use"u8.ToArray();
    private static readonly byte[] Inventory = "inventory"u8.ToArray();
    private static readonly byte[] Map = "map"u8.ToArray();
    private static readonly byte[] MenuConfirm = "menu.confirm"u8.ToArray();
    private static readonly byte[] MenuCancel = "menu.cancel"u8.ToArray();
    private static readonly byte[] MenuUp = "menu.up"u8.ToArray();
    private static readonly byte[] MenuDown = "menu.down"u8.ToArray();

    private bool _forward;
    private bool _back;
    private bool _left;
    private bool _right;
    private bool _attackHeld;
    private bool _usePressed;
    private bool _inventoryToggled;
    private bool _mapToggled;
    private bool _menuConfirm;
    private bool _menuCancel;
    private bool _menuUp;
    private bool _menuDown;
    private int _hotbarPressed;
    private float _lookYaw;
    private float _lookPitch;

    /// <summary>Route one update's events into the pending tick state.</summary>
    public void Route(ReadOnlySpan<ProductInputEvent> events)
    {
        foreach (ProductInputEvent input in events)
        {
            switch (input.Kind)
            {
                case InputEventKind.Clear:
                    ReleaseAll();
                    continue;
                case InputEventKind.PointerDelta:
                    _lookYaw += input.X * LookSensitivity;
                    _lookPitch += input.Y * LookSensitivity;
                    continue;
                case InputEventKind.MappedDigital:
                case InputEventKind.DirectDigital:
                    RouteDigital(input);
                    continue;
            }
        }
    }

    private void RouteDigital(ProductInputEvent input)
    {
        bool active = input.X > DigitalThreshold;
        bool pressed = active && input.Edge is InputEdge.Pressed or InputEdge.Held or InputEdge.None;
        bool released = input.Edge == InputEdge.Released || !active;

        if (Matches(input, MoveForward))
        {
            _forward = pressed;
        }
        else if (Matches(input, MoveBack))
        {
            _back = pressed;
        }
        else if (Matches(input, MoveLeft))
        {
            _left = pressed;
        }
        else if (Matches(input, MoveRight))
        {
            _right = pressed;
        }
        else if (Matches(input, Attack))
        {
            _attackHeld = !released;
        }
        else if (released)
        {
            return;
        }
        else if (Matches(input, Use))
        {
            _usePressed = true;
        }
        else if (Matches(input, Inventory))
        {
            _inventoryToggled = true;
        }
        else if (Matches(input, Map))
        {
            _mapToggled = true;
        }
        else if (Matches(input, MenuConfirm))
        {
            _menuConfirm = true;
        }
        else if (Matches(input, MenuCancel))
        {
            _menuCancel = true;
        }
        else if (Matches(input, MenuUp))
        {
            _menuUp = true;
        }
        else if (Matches(input, MenuDown))
        {
            _menuDown = true;
        }
        else if (input.Intent.Length > 0)
        {
            int slot = HotbarSlot(input.Intent.Span);
            if (slot > 0)
            {
                _hotbarPressed = slot;
            }
        }
    }

    private static int HotbarSlot(ReadOnlySpan<byte> intent)
    {
        ReadOnlySpan<byte> prefix = "hotbar."u8;
        if (intent.Length != prefix.Length + 1 || !intent[..prefix.Length].SequenceEqual(prefix))
        {
            return 0;
        }

        return intent[^1] - (byte)'0';
    }

    private static bool Matches(ProductInputEvent input, ReadOnlySpan<byte> intent) =>
        input.Intent.Span.SequenceEqual(intent);

    /// <summary>Consume the pending state into one tick of run input.</summary>
    public RunInput TakeTickInput()
    {
        var input = new RunInput(
            MoveX: (_right ? 1f : 0f) - (_left ? 1f : 0f),
            MoveY: (_forward ? 1f : 0f) - (_back ? 1f : 0f),
            LookYawDegrees: _lookYaw,
            LookPitchDegrees: _lookPitch,
            AttackHeld: _attackHeld,
            UsePressed: _usePressed,
            HotbarPressed: _hotbarPressed,
            InventoryToggled: _inventoryToggled,
            MapToggled: _mapToggled,
            MenuConfirm: _menuConfirm,
            MenuCancel: _menuCancel,
            MenuUp: _menuUp,
            MenuDown: _menuDown);

        _lookYaw = 0f;
        _lookPitch = 0f;
        _usePressed = false;
        _hotbarPressed = 0;
        _inventoryToggled = false;
        _mapToggled = false;
        _menuConfirm = false;
        _menuCancel = false;
        _menuUp = false;
        _menuDown = false;
        return input;
    }

    private void ReleaseAll()
    {
        _forward = false;
        _back = false;
        _left = false;
        _right = false;
        _attackHeld = false;
    }
}
