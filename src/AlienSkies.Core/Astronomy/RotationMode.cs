using System.ComponentModel;

namespace AlienSkies.Core.Astronomy;

public enum RotationMode
{
    [Description("Auto (locked if orbit < 0.1 AU)")] Auto,
    TidallyLocked,
    Free,
}
