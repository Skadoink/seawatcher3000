using System;
using System.Collections.Generic;

namespace seawatcher3000
{
    public class Track
    {
        public string DateID = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        public List<(float X, float Y, DateTime TimeStamp)> Positions { get; } = new();
        public List<(float Width, float Height)> BoundingSizes { get; } = new();

        public void Reset()
        {
            Positions.Clear();
            BoundingSizes.Clear();
            DateID = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }
}

