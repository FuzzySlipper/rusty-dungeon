using DelveRpg.Host.Input;
using Rusty.Engine;

namespace DelveRpg.Host.Tests;

public sealed class DelveInputRouterTests
{
    private static ProductInputEvent Digital(string intent, InputEdge edge, float x) => new(
        Kind: InputEventKind.MappedDigital,
        Edge: edge,
        Device: default,
        Channel: default,
        Axis: default,
        Keyboard: default,
        PointerButton: default,
        ControllerButton: default,
        ControllerAxis: default,
        ClearReason: default,
        ValueKind: InputValueKind.Digital,
        Phase: default,
        Provenance: default,
        Binding: default,
        Sequence: default,
        Context: default,
        X: x,
        Y: 0f,
        Label: default,
        MappingId: default,
        Intent: System.Text.Encoding.ASCII.GetBytes(intent),
        PayloadContract: default,
        PayloadData: default);

    private static ProductInputEvent Clear(InputClearReason reason) => new(
        Kind: InputEventKind.Clear,
        Edge: default,
        Device: default,
        Channel: default,
        Axis: default,
        Keyboard: default,
        PointerButton: default,
        ControllerButton: default,
        ControllerAxis: default,
        ClearReason: reason,
        ValueKind: default,
        Phase: default,
        Provenance: default,
        Binding: default,
        Sequence: default,
        Context: default,
        X: 0f,
        Y: 0f,
        Label: default,
        MappingId: default,
        Intent: default,
        PayloadContract: default,
        PayloadData: default);

    private static ProductInputEvent Look(float yaw, float pitch) => new(
        Kind: InputEventKind.PointerDelta,
        Edge: default,
        Device: default,
        Channel: default,
        Axis: default,
        Keyboard: default,
        PointerButton: default,
        ControllerButton: default,
        ControllerAxis: default,
        ClearReason: default,
        ValueKind: default,
        Phase: default,
        Provenance: default,
        Binding: default,
        Sequence: default,
        Context: default,
        X: yaw,
        Y: pitch,
        Label: default,
        MappingId: default,
        Intent: default,
        PayloadContract: default,
        PayloadData: default);

    [Fact]
    public void Held_intents_persist_while_the_engine_keeps_asserting_them()
    {
        var router = new DelveInputRouter();
        router.Route(new[] { Digital("move.forward", InputEdge.Held, 1f) });
        Assert.Equal(1f, router.TakeTickInput().MoveY);

        router.Route(new[] { Digital("move.forward", InputEdge.Held, 1f) });
        Assert.Equal(1f, router.TakeTickInput().MoveY);
    }

    [Fact]
    public void Held_intents_release_by_absence_not_by_a_release_edge()
    {
        // Held-trigger mappings emit nothing on release; a missing assertion
        // must release the hold (a latched press reads as an auto-repeater).
        var router = new DelveInputRouter();
        router.Route(new[] { Digital("attack", InputEdge.Held, 1f) });
        Assert.True(router.TakeTickInput().AttackHeld);

        router.Route(ReadOnlySpan<ProductInputEvent>.Empty);
        Assert.False(router.TakeTickInput().AttackHeld);
    }

    [Fact]
    public void An_explicit_release_also_releases_the_hold()
    {
        var router = new DelveInputRouter();
        router.Route(new[] { Digital("move.forward", InputEdge.Pressed, 1f) });
        Assert.Equal(1f, router.TakeTickInput().MoveY);

        router.Route(new[] { Digital("move.forward", InputEdge.Released, 0f) });
        Assert.Equal(0f, router.TakeTickInput().MoveY);
    }

    [Fact]
    public void One_shot_intents_fire_once_per_press()
    {
        var router = new DelveInputRouter();
        router.Route(new[] { Digital("use", InputEdge.Pressed, 1f) });
        Assert.True(router.TakeTickInput().UsePressed);
        Assert.False(router.TakeTickInput().UsePressed);
    }

    [Fact]
    public void Hotbar_intents_route_to_their_slot()
    {
        var router = new DelveInputRouter();
        router.Route(new[] { Digital("hotbar.3", InputEdge.Pressed, 1f) });
        Assert.Equal(3, router.TakeTickInput().HotbarPressed);

        // Belt expansions reach ten slots; the tenth is the 0 key.
        router.Route(new[] { Digital("hotbar.9", InputEdge.Pressed, 1f) });
        Assert.Equal(9, router.TakeTickInput().HotbarPressed);
        router.Route(new[] { Digital("hotbar.0", InputEdge.Pressed, 1f) });
        Assert.Equal(10, router.TakeTickInput().HotbarPressed);
    }

    [Fact]
    public void Clear_releases_every_derived_state()
    {
        var router = new DelveInputRouter();
        ProductInputEvent[] batch =
        [
            Digital("move.forward", InputEdge.Pressed, 1f),
            Digital("attack", InputEdge.Pressed, 1f),
            Digital("use", InputEdge.Pressed, 1f),
            Digital("menu.confirm", InputEdge.Pressed, 1f),
            Digital("hotbar.2", InputEdge.Pressed, 1f),
            Digital("inventory", InputEdge.Pressed, 1f),
            Digital("map", InputEdge.Pressed, 1f),
            Digital("menu.cancel", InputEdge.Pressed, 1f),
            Digital("menu.up", InputEdge.Pressed, 1f),
            Digital("menu.down", InputEdge.Pressed, 1f),
            Look(1.2f, 0.6f),
            Clear(InputClearReason.FocusLoss),
        ];
        router.Route(batch);

        var input = router.TakeTickInput();
        Assert.Equal(0f, input.MoveY);
        Assert.Equal(0f, input.MoveX);
        Assert.False(input.AttackHeld);
        Assert.False(input.UsePressed);
        Assert.False(input.MenuConfirm);
        Assert.False(input.MenuCancel);
        Assert.False(input.MenuUp);
        Assert.False(input.MenuDown);
        Assert.False(input.InventoryToggled);
        Assert.False(input.MapToggled);
        Assert.Equal(0, input.HotbarPressed);
        Assert.Equal(0f, input.LookYawDegrees);
        Assert.Equal(0f, input.LookPitchDegrees);
    }

    [Fact]
    public void Look_deltas_accumulate_across_events_and_drain_once()
    {
        var router = new DelveInputRouter();
        router.Route(new[] { Look(1.5f, -0.5f), Look(0.5f, 0.25f) });
        var input = router.TakeTickInput();
        // Units integrate through the Engine Look service at 0.12° per unit,
        // with the SDK's InvertVertical convention: moving the mouse up
        // (negative Y) pitches the camera up (positive degrees).
        Assert.True(MathF.Abs(input.LookYawDegrees - (2f * 0.12f)) < 1e-4f);
        Assert.True(MathF.Abs(input.LookPitchDegrees - (0.25f * 0.12f)) < 1e-4f);
        Assert.Equal(0f, router.TakeTickInput().LookYawDegrees);
    }

    [Fact]
    public void Unmapped_event_kinds_are_ignored_without_sticking()
    {
        var router = new DelveInputRouter();
        ProductInputEvent payload = Digital("use", InputEdge.Pressed, 1f) with
        {
            Kind = InputEventKind.DirectProductPayload,
        };
        router.Route(new[] { payload });
        Assert.False(router.TakeTickInput().UsePressed);
    }
}
