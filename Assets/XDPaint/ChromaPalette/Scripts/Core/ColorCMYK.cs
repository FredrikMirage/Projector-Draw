namespace XDPaint.ChromaPalette.Core
{
    /// <summary>
    /// CMYK color representation.
    /// </summary>
    public struct ColorCMYK
    {
        public float C, M, Y, K;
        
        public ColorCMYK(float c, float m, float y, float k)
        {
            C = c;
            M = m;
            Y = y;
            K = k;
        }
    }
}