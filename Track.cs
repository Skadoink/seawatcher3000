using System;
using System.Collections.Generic;
public class Engagement
{
    public int ConsecutiveDetections { get; set; }
    public List<(float X, float Y)> Positions { get; } = new();

    public double? LastSaveTime { get; set; }
    public double SaveGapSeconds { get; set; } = 0.1;

    public void Reset()
    {
        ConsecutiveDetections = 0;
        Positions.Clear();
        LastSaveTime = null;
        SaveGapSeconds = 0.1;
    }
}
