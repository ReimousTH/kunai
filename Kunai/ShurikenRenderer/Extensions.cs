using Kunai.ShurikenRenderer;
using SharpNeedle;
using SharpNeedle.Framework.Ninja.Csd;
using SharpNeedle.Structs;
using Shuriken.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

public static class ExtensionKillMe
{
    public static System.Numerics.Vector4 ToVec4(this Color<byte> in_Value)
    {
        return new System.Numerics.Vector4(in_Value.R / 255.0f, in_Value.G / 255.0f, in_Value.B / 255.0f, in_Value.A / 255.0f);
    }
    public static Vector2 Rotate(this Vector2 in_V, float in_Angle)
    {
        return new Vector2(
            in_V.X * MathF.Cos(in_Angle) + in_V.Y * MathF.Sin(in_Angle),
            in_V.Y * MathF.Cos(in_Angle) - in_V.X * MathF.Sin(in_Angle));
    }
    public static Vector2 RotatePoint(this Vector2 in_Point, float in_Radians)
    {
        float cosTheta = (float)Math.Cos(in_Radians);
        float sinTheta = (float)Math.Sin(in_Radians);
        return new Vector2(
            in_Point.X * cosTheta - in_Point.Y * sinTheta,
            in_Point.X * sinTheta + in_Point.Y * cosTheta
        );
    }
    public static System.Numerics.Vector4 Invert(this System.Numerics.Vector4 in_Value)
    {
        /// TODO: SHARPNEEDLE FIX
        /// There is a "bug" in SharpNeedle where it doesn't consider
        /// that YNCPs and other big endian files have an inverted color order
        /// 

            float fixA = in_Value.X;
            float fixR = in_Value.Y;
            float fixG = in_Value.Z;
            float fixB = in_Value.W;
            return new System.Numerics.Vector4(fixB, fixG, fixR, fixA);
    }
    public static double Magnitude(this Color<byte> in_Value)
    {
        return Math.Sqrt(in_Value.R * in_Value.R + in_Value.G * in_Value.G + in_Value.B * in_Value.B + in_Value.A * in_Value.A);
    }
    public static Color<byte> ToSharpNeedleColor(this System.Numerics.Vector4 in_Value)
    {
        return new Color<byte>((byte)(in_Value.X * 255), (byte)(in_Value.Y * 255), (byte)(in_Value.Z * 255), (byte)(in_Value.W * 255));
    }

}
public static class AnimationTypeMethods
{
    public static bool IsColor(this AnimationType in_Type)
    {
        return new AnimationType[] {
                AnimationType.Color,
                AnimationType.GradientTl,
                AnimationType.GradientBl,
                AnimationType.GradientTr,
                AnimationType.GradientBr
            }.Contains(in_Type);
    }
    public static int FindKeyframe(this SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrameList in_List, float in_Frame)
    {
        int min = 0;
        int max = in_List.Count - 1;

        while (min <= max)
        {
            int index = (min + max) / 2;

            if (in_Frame < in_List[index].Frame)
                max = index - 1;
            else
                min = index + 1;

        }

        return min;
    }
    public static int FindKeyframeStrict(this SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrameList in_List, float in_Frame)
    {
        int min = 0;
        int max = in_List.Count - 1;

        while (min <= max)
        {
            int index = (min + max) / 2;

            if ((uint)in_Frame < in_List[index].Frame)
                max = index - 1;
            else if ((uint)in_Frame > in_List[index].Frame)
                min = index + 1;
            else return index;
        }

        return -1;
    }
    public static float GetSingle(this SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrameList in_List, float in_Frame)
    {
        if (in_List.Count == 0)
            return 0.0f;

        if (in_Frame >= in_List[^1].Frame)
            return in_List[^1].Value.Float;

        int index = in_List.FindKeyframe(in_Frame);

        if (index == 0)
            return in_List.Frames[index].Value.Float;


        var keyframe = in_List.Frames[index - 1];
        var nextKeyframe = in_List.Frames[index];



        float factor;

        if (nextKeyframe.Frame - keyframe.Frame > 0)
            factor = (in_Frame - keyframe.Frame) / (nextKeyframe.Frame - keyframe.Frame);
        else
            factor = 0.0f;

        switch (keyframe.Interpolation)
        {
            case SharpNeedle.Framework.Ninja.Csd.Motions.InterpolationType.Linear:
                return (1.0f - factor) * keyframe.Value.Float + nextKeyframe.Value.Float * factor;

            case SharpNeedle.Framework.Ninja.Csd.Motions.InterpolationType.Hermite:
                float valueDelta = nextKeyframe.Value.Float - keyframe.Value.Float;
                float frameDelta = nextKeyframe.Frame - keyframe.Frame;

                float biasSquaric = factor * factor;
                float biasCubic = biasSquaric * factor;

                float valueCubic = (keyframe.OutTangent + keyframe.InTangent) * frameDelta - valueDelta * 2.0f;
                float valueSquaric = valueDelta * 3.0f - (keyframe.InTangent * 2.0f + keyframe.OutTangent) * frameDelta;
                float valueLinear = frameDelta * keyframe.InTangent;

                return valueCubic * biasCubic + valueSquaric * biasSquaric + valueLinear * factor + keyframe.Value.Float;

            default:
                return keyframe.Value.Float;
        }
    }
    public static DrawType GetDrawType(this Cast in_Cast)
    {
        return (DrawType)in_Cast.Field04;
    }
    public static Vector4 GetColor(this SharpNeedle.Framework.Ninja.Csd.Motions.KeyFrameList in_List, float in_Frame)
    {
        if (in_List.Count == 0)
            return new Vector4();

        if (in_Frame >= in_List.Frames[^1].Frame)
            return in_List.Frames[^1].Value.Color.ToVec4();

        int index = in_List.FindKeyframe(in_Frame);

        if (index == 0)
            return in_List.Frames[index].Value.Color.ToVec4();

        var keyframe = in_List.Frames[index - 1];
        var nextKeyframe = in_List.Frames[index];  

        float factor;

        if (nextKeyframe.Frame - keyframe.Frame > 0)
            factor = (in_Frame - keyframe.Frame) / (nextKeyframe.Frame - keyframe.Frame);
        else
            factor = 0.0f;

        // Color values always use linear interpolation regardless of the type.
        var swappedCurrent = keyframe.Value.Color;
        var swappedNext = nextKeyframe.Value.Color;
        return new Color<byte>
        {
            R = (byte)((1.0f - factor) * swappedCurrent.R + swappedNext.R * factor),
            G = (byte)((1.0f - factor) * swappedCurrent.G + swappedNext.G * factor),
            B = (byte)((1.0f - factor) * swappedCurrent.B + swappedNext.B * factor),
            A = (byte)((1.0f - factor) * swappedCurrent.A + swappedNext.A * factor)
        }.ToVec4();
    }
    public static AnimationType ToShurikenAnimationType(this SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty in_Test)
    {
        switch (in_Test)
        {
            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.HideFlag:
                return AnimationType.HideFlag;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionX:
                return AnimationType.XPosition;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.PositionY:
                return AnimationType.YPosition;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.Rotation:
                return AnimationType.Rotation;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleX:
                return AnimationType.XScale;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.ScaleY:
                return AnimationType.YScale;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.SpriteIndex:
                return AnimationType.SubImage;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.Color:
                return AnimationType.Color;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientTopLeft:
                return AnimationType.GradientTl;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientBottomLeft:
                return AnimationType.GradientBl;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientTopRight:
                return AnimationType.GradientTr;

            case SharpNeedle.Framework.Ninja.Csd.Motions.KeyProperty.GradientBottomRight:
                return AnimationType.GradientBr;

        }
        return AnimationType.None;
    }
}