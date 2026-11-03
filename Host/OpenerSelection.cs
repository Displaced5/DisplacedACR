using ViperACR.Core;

namespace ViperACR.Host;

public static class OpenerSelection
{
    // Display order is independent of persisted enum values.
    public static readonly string[] Labels = [StandardOpener.Name, DsrOpener.Name, TeaOpener.Name];
    public static int ToIndex(OpenerMode mode) => mode switch {
        OpenerMode.Dsr90 => 1, OpenerMode.Tea80 => 2, _ => 0
    };
    public static OpenerMode FromIndex(int index) => index switch {
        1 => OpenerMode.Dsr90, 2 => OpenerMode.Tea80, _ => OpenerMode.Standard
    };
}
