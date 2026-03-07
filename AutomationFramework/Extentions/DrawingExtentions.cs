using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices.Swift;

namespace AutomationFramework.Extensions;

public static class DrawingExtensions
{
    public static Vector2 Center(this Rectangle rect)
        => new Vector2(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);

    public static Vector2 Center(this RectangleF rect)
        => new Vector2(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);


    public static Rectangle Inset(this Rectangle rect, int insetX, int? insetY = null)
    {
        insetY ??= insetX;

        return new Rectangle(
            rect.Left + insetX,
            rect.Top + insetY.Value,
            rect.Width - insetX,
            rect.Height - insetY.Value
        );
    }

    public static RectangleF Inset(this RectangleF rect, float insetX, float? insetY = null)
    {
        insetY ??= insetX;

        return new RectangleF(
            rect.Left + insetX,
            rect.Top + insetY.Value,
            rect.Width - insetX,
            rect.Height - insetY.Value
        );
    }

    public static Rectangle Padd(this Rectangle rect, int paddingX, int? paddingY = null, Vector2? anchor = null)
    {
        paddingY ??= paddingX;
        anchor ??= new Vector2(0.5f, 0.5f);

        int x = (int)MathF.Round(rect.X - (paddingX * anchor.Value.X));
        int y = (int)MathF.Round(rect.Y - (paddingY.Value * anchor.Value.Y));
        int width = rect.Width + paddingX;
        int height = rect.Height + paddingY.Value;

        return new Rectangle(x, y, width, height);
    }


    public static RectangleF Padd(this RectangleF rect, float paddingX, float? paddingY = null, Vector2? anchor = null)
    {
        paddingY ??= paddingX;
        anchor ??= new Vector2(0.5f, 0.5f);

        float x = rect.X - (paddingX * anchor.Value.X);
        float y = rect.Y - (paddingY.Value * anchor.Value.Y);
        float width = rect.Width + paddingX;
        float height = rect.Height + paddingY.Value;

        return new RectangleF(x, y, width, height);
    }


    public static Vector2 GetRandomPointInBounds(this RectangleF rect)
    {
        float x = rect.Left + Random.Shared.NextFloat(0, rect.Width);
        float y = rect.Top + Random.Shared.NextFloat(0, rect.Height);

        return new Vector2(x, y);
    }

    public static Vector2 GetRandomPointInBounds(this Rectangle rect)
    {
        float x = rect.Left + Random.Shared.NextFloat(0, rect.Width);
        float y = rect.Top + Random.Shared.NextFloat(0, rect.Height);

        return new Vector2(x, y);
    }
}