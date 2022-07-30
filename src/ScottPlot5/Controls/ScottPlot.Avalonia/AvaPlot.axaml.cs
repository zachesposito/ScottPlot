﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Visuals.Media.Imaging;
using ScottPlot.Control;
using SkiaSharp;

namespace ScottPlot.Avalonia
{
    public partial class AvaPlot : UserControl, IPlotControl
    {
        public Plot Plot { get; } = new();

        public Backend Backend { get; set; }

        public AvaPlot()
        {
            InitializeComponent();
            Backend = new(this);

            Refresh();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void Render(DrawingContext context)
        {
            SKImageInfo imageInfo = new((int)Bounds.Width, (int)Bounds.Height);

            using var surface = SKSurface.Create(imageInfo);
            if (surface is null)
                return;

            Plot.Render(surface);

            // TODO: can this sequence be improved to reduce copying?
            SKImage img = surface.Snapshot();
            SKPixmap pixels = img.ToRasterImage().PeekPixels();
            byte[] bytes = pixels.GetPixelSpan().ToArray();

            using WriteableBitmap bmp = new(
                size: new global::Avalonia.PixelSize((int)Bounds.Width, (int)Bounds.Height),
                dpi: new Vector(1, 1),
                format: PixelFormat.Bgra8888,
                alphaFormat: AlphaFormat.Unpremul);

            using ILockedFramebuffer buf = bmp.Lock();
            {
                Marshal.Copy(bytes, 0, buf.Address, pixels.BytesSize);
            }

            Rect rect = new(0, 0, Bounds.Width, Bounds.Height);

            context.DrawImage(bmp, rect, rect, BitmapInterpolationMode.HighQuality);
        }

        public void Refresh()
        {
            InvalidateVisual();
        }

        // TODO: should every On event call its base event???
        private void OnMouseDown(object sender, PointerPressedEventArgs e)
        {
            Backend.MouseDown(
                position: e.ToPixel(this),
                button: e.GetCurrentPoint(this).Properties.PointerUpdateKind.ToButton());

            e.Pointer.Capture(this);

            if (e.ClickCount == 2)
            {
                Backend.DoubleClick();
            }
        }

        private void OnMouseUp(object sender, PointerReleasedEventArgs e)
        {
            Backend.MouseUp(
                position: e.ToPixel(this),
                button: e.GetCurrentPoint(this).Properties.PointerUpdateKind.ToButton());

            e.Pointer.Capture(null);
        }

        private void OnMouseMove(object sender, PointerEventArgs e)
        {
            Backend.MouseMove(e.ToPixel(this));
        }

        private void OnMouseWheel(object sender, PointerWheelEventArgs e)
        {
            // Avalonia flips the delta vector when shift is held. This is seemingly intentional: https://github.com/AvaloniaUI/Avalonia/pull/7520
            float delta = (float)(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? e.Delta.X : e.Delta.Y);

            if (delta != 0)
            {
                Backend.MouseWheelVertical(e.ToPixel(this), delta);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            Backend.KeyDown(e.ToKey());
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            Backend.KeyUp(e.ToKey());
        }
    }
}