using System.Drawing;
using System.Drawing.Drawing2D;

namespace AppStarter.Helpers;

/// <summary>
/// Generates a banana icon programmatically for the system tray
/// </summary>
public static class IconGenerator
{
    public static Icon CreateBananaIcon(int size = 64)
    {
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        
        // Scale factor
        float scale = size / 64f;
        
        // Banana body (curved yellow shape)
        using var bananaPath = new GraphicsPath();
        
        // Main banana curve
        bananaPath.AddBezier(
            new PointF(12 * scale, 50 * scale),  // Start
            new PointF(8 * scale, 30 * scale),   // Control 1
            new PointF(20 * scale, 8 * scale),   // Control 2
            new PointF(52 * scale, 12 * scale)   // End
        );
        
        // Top curve
        bananaPath.AddBezier(
            new PointF(52 * scale, 12 * scale),  // Start
            new PointF(58 * scale, 14 * scale),  // Control 1
            new PointF(58 * scale, 22 * scale),  // Control 2
            new PointF(52 * scale, 24 * scale)   // End
        );
        
        // Return curve (bottom)
        bananaPath.AddBezier(
            new PointF(52 * scale, 24 * scale),  // Start
            new PointF(28 * scale, 22 * scale),  // Control 1
            new PointF(18 * scale, 38 * scale),  // Control 2
            new PointF(20 * scale, 52 * scale)   // End
        );
        
        bananaPath.CloseFigure();
        
        // Yellow gradient fill
        using var gradientBrush = new LinearGradientBrush(
            new Point(0, 0),
            new Point(size, size),
            Color.FromArgb(255, 255, 220, 80),   // Light yellow
            Color.FromArgb(255, 255, 200, 50)    // Darker yellow
        );
        
        g.FillPath(gradientBrush, bananaPath);
        
        // Brown tip
        using var tipBrush = new SolidBrush(Color.FromArgb(255, 139, 90, 43));
        g.FillEllipse(tipBrush, 
            48 * scale, 10 * scale, 
            10 * scale, 10 * scale);
        
        // Brown stem
        using var stemBrush = new SolidBrush(Color.FromArgb(255, 101, 67, 33));
        g.FillRectangle(stemBrush,
            16 * scale, 48 * scale,
            8 * scale, 6 * scale);
        
        // Outline
        using var outlinePen = new Pen(Color.FromArgb(255, 180, 140, 40), 2 * scale);
        g.DrawPath(outlinePen, bananaPath);
        
        // Create icon from bitmap
        return Icon.FromHandle(bitmap.GetHicon());
    }
    
    public static Icon CreateSimpleBananaIcon(int size = 32)
    {
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        
        float scale = size / 32f;
        
        // Simple banana shape using ellipse rotated
        g.TranslateTransform(size / 2f, size / 2f);
        g.RotateTransform(-30);
        g.TranslateTransform(-size / 2f, -size / 2f);
        
        // Main banana body
        using var yellowBrush = new SolidBrush(Color.FromArgb(255, 255, 225, 50));
        g.FillEllipse(yellowBrush, 4 * scale, 8 * scale, 24 * scale, 14 * scale);
        
        // Brown ends
        using var brownBrush = new SolidBrush(Color.FromArgb(255, 101, 67, 33));
        g.FillEllipse(brownBrush, 2 * scale, 12 * scale, 5 * scale, 6 * scale);
        g.FillEllipse(brownBrush, 25 * scale, 12 * scale, 5 * scale, 6 * scale);
        
        // Outline
        using var outlinePen = new Pen(Color.FromArgb(255, 200, 160, 40), 1.5f * scale);
        g.DrawEllipse(outlinePen, 4 * scale, 8 * scale, 24 * scale, 14 * scale);
        
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
