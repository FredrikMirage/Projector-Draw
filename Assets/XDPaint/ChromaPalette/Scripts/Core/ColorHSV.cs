namespace XDPaint.ChromaPalette.Core
{
    /// <summary>
    /// HSV color representation.
    /// </summary>
    public struct ColorHSV
    {
        public float H, S, V, A;
        
        public ColorHSV(float h, float s, float v, float a = 1f)
        {
            H = h;
            S = s;
            V = v;
            A = a;
        }
    }
}