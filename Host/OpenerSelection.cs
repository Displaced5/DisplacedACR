using ViperACR.Core;

namespace ViperACR.Host;

public static class OpenerSelection
{
    // Display order is independent of persisted enum values.
    public static readonly string[] Labels = ["100级标准起手", FruOpener.Name, DsrOpener.Name, TeaOpener.Name];
    public static int ToIndex(OpenerMode mode) => mode switch {
        OpenerMode.Fru100 => 1, OpenerMode.Dsr90 => 2, OpenerMode.Tea80 => 3, _ => 0
    };
    public static OpenerMode FromIndex(int index) => index switch {
        1 => OpenerMode.Fru100, 2 => OpenerMode.Dsr90, 3 => OpenerMode.Tea80, _ => OpenerMode.Standard
    };
}
