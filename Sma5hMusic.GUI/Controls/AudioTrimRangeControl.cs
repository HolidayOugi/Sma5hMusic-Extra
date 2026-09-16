using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace Sma5hMusic.GUI.Controls
{
    public class AudioTrimRangeControl : Control
    {
        //waveform rendering constants
        private const double TrackPadding = 8;
        private const double ThumbWidth = 1;
        private const double VerticalPadding = 6;

        private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.Parse("#401E88E5"));
        private static readonly IBrush OutsideWaveformBrush = new SolidColorBrush(Color.Parse("#66808080"));
        private static readonly IBrush SelectedWaveformBrush = new SolidColorBrush(Color.Parse("#D9FFFFFF"));
        private static readonly IBrush HandleBrush = new SolidColorBrush(Color.Parse("#B0168CE0"));
        private static readonly Pen OutsideWaveformPen = new Pen(OutsideWaveformBrush, 1);
        private static readonly Pen SelectedWaveformPen = new Pen(SelectedWaveformBrush, 1.15);
        private static readonly Pen SelectionOutlinePen = new Pen(HandleBrush, 1.25);

        private DraggedThumb _draggedThumb;

        public static readonly StyledProperty<uint> MinimumProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(Minimum));

        public static readonly StyledProperty<uint> MaximumProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(Maximum), 1);

        public static readonly StyledProperty<uint> StartValueProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(
                nameof(StartValue),
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<uint> EndValueProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(
                nameof(EndValue),
                1,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<float[]> WaveformPeaksProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, float[]>(
                nameof(WaveformPeaks),
                Array.Empty<float>());

        static AudioTrimRangeControl()
        {
            AffectsRender<AudioTrimRangeControl>(
                MinimumProperty,
                MaximumProperty,
                StartValueProperty,
                EndValueProperty,
                WaveformPeaksProperty);
        }

        public uint Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public uint Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public uint StartValue
        {
            get => GetValue(StartValueProperty);
            set => SetValue(StartValueProperty, value);
        }

        public uint EndValue
        {
            get => GetValue(EndValueProperty);
            set => SetValue(EndValueProperty, value);
        }

        public float[] WaveformPeaks
        {
            get => GetValue(WaveformPeaksProperty);
            set => SetValue(WaveformPeaksProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var trackLeft = TrackPadding;
            var trackWidth = Math.Max(1, Bounds.Width - (TrackPadding * 2));
            var startX = ValueToX(StartValue, trackLeft, trackWidth);
            var endX = ValueToX(EndValue, trackLeft, trackWidth);
            var waveformTop = VerticalPadding;
            var waveformHeight = Math.Max(1, Bounds.Height - (VerticalPadding * 2));
            var selectionRect = new Rect(startX, waveformTop, Math.Max(0, endX - startX), waveformHeight);

            context.FillRectangle(SelectionBrush, selectionRect);
            DrawWaveform(context, OutsideWaveformPen, trackLeft, trackWidth, waveformTop, waveformHeight);

            using (context.PushClip(selectionRect))
                DrawWaveform(context, SelectedWaveformPen, trackLeft, trackWidth, waveformTop, waveformHeight);

            context.DrawLine(SelectionOutlinePen, new Point(startX, waveformTop), new Point(endX, waveformTop));
            context.DrawLine(SelectionOutlinePen, new Point(startX, waveformTop + waveformHeight), new Point(endX, waveformTop + waveformHeight));
            context.FillRectangle(
                HandleBrush,
                new Rect(startX - (ThumbWidth / 2), waveformTop, ThumbWidth, waveformHeight));
            context.FillRectangle(
                HandleBrush,
                new Rect(endX - (ThumbWidth / 2), waveformTop, ThumbWidth, waveformHeight));
        }

        private void DrawWaveform(
            DrawingContext context,
            Pen pen,
            double left,
            double width,
            double top,
            double height)
        {
            var peaks = WaveformPeaks;
            var centerY = top + (height / 2);
            if (peaks == null || peaks.Length == 0)
            {
                context.DrawLine(pen, new Point(left, centerY), new Point(left + width, centerY));
                return;
            }

            var halfHeight = height / 2;
            for (var index = 0; index < peaks.Length; index++)
            {
                var x = left + (index / (double)Math.Max(1, peaks.Length - 1) * width);
                var amplitude = Math.Max(0, Math.Min(1, peaks[index])) * halfHeight;
                context.DrawLine(
                    pen,
                    new Point(x, centerY - amplitude),
                    new Point(x, centerY + amplitude));
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetPosition(this);
            var trackLeft = TrackPadding;
            var trackWidth = Math.Max(1, Bounds.Width - (TrackPadding * 2));
            var startX = ValueToX(StartValue, trackLeft, trackWidth);
            var endX = ValueToX(EndValue, trackLeft, trackWidth);

            _draggedThumb = Math.Abs(point.X - startX) <= Math.Abs(point.X - endX)
                ? DraggedThumb.Start
                : DraggedThumb.End;
            e.Pointer.Capture(this);
            UpdateValueFromPointer(point.X);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_draggedThumb == DraggedThumb.None)
                return;

            UpdateValueFromPointer(e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_draggedThumb == DraggedThumb.None)
                return;

            UpdateValueFromPointer(e.GetPosition(this).X);
            _draggedThumb = DraggedThumb.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void UpdateValueFromPointer(double x)
        {
            var maximum = Math.Max(Minimum + 1u, Maximum);
            var trackWidth = Math.Max(1, Bounds.Width - (TrackPadding * 2));
            var ratio = Math.Max(0, Math.Min(1, (x - TrackPadding) / trackWidth));
            var value = Minimum + (uint)Math.Round(ratio * (maximum - Minimum));

            if (_draggedThumb == DraggedThumb.Start)
            {
                var latestStart = EndValue == 0 ? 0 : EndValue - 1;
                SetValue(StartValueProperty, Math.Min(value, latestStart));
            }
            else if (_draggedThumb == DraggedThumb.End)
            {
                var earliestEnd = StartValue == uint.MaxValue ? uint.MaxValue : StartValue + 1;
                SetValue(EndValueProperty, Math.Max(value, earliestEnd));
            }
        }

        private double ValueToX(uint value, double trackLeft, double trackWidth)
        {
            if (Maximum <= Minimum)
                return trackLeft;

            var clamped = Math.Max(Minimum, Math.Min(Maximum, value));
            return trackLeft + ((clamped - Minimum) / (double)(Maximum - Minimum) * trackWidth);
        }

        private enum DraggedThumb
        {
            None,
            Start,
            End
        }
    }
}
