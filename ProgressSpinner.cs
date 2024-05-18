using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WpfProgressSpinner
{
    // TODO : Implementation of each state -> None, Completed, Paused, Error
    public enum ProgressState
    {
        None = 0,
        Indeterminate = 1,
        Normal = 2,
        Completed = 3,
        Paused = 4,
        Error = 5
    }

    [TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
    [TemplatePart(Name = "PART_Indicator", Type = typeof(FrameworkElement))]
    public sealed class ProgressSpinner : RangeBase
    {

        #region Data

        private const double ANIMATION_DURATION_BASIS = 1.5;

        private const string TrackTemplateName = "PART_Track";
        private const string IndicatorTemplateName = "PART_Indicator";
        private static ProgressState _defaultState = ProgressState.None;

        private ScaleTransform _scaleTransform = new ScaleTransform();
        private AnimationClock _indicatorAnimationClock;

        private FrameworkElement _track;
        private FrameworkElement _indicator;

        #endregion Data

        static ProgressSpinner()
        {
            // Override dependency properties

            FocusableProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(false));
            
            // Set default to 100.0
            MaximumProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(100.0, (d, e) => {
                ((ProgressSpinner)d).UpdateIndicator();
            }));

            // Set default to 0.0
            MinimumProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(0.0, (d, e) => {
                ((ProgressSpinner)d).UpdateIndicator();
            }));

            // Set default size properties
            WidthProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(40.0));
            HeightProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(40.0));

            StyleProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(CreateStyle()));

            // Create a [PathGeometry] for [PointAnimationgUsingPath]
            // It is the base curve for the spinner arc animation calculations.
        }

        public ProgressSpinner()
        {
            var animationSpeedBinding = new Binding("AnimationSpeedRatio")
            {
                Source = this,
                Mode = BindingMode.OneWay,
            };

            Loaded += (s, e) =>
            {
                UpdateAnimation();
            };

            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    UpdateAnimation();
                }
                else
                {
                    StopIndicatorAnimation();
                }
            };
        }


        private void UpdateIndicator()
        {
            SetCurrentValue(ProgressRatioProperty, Maximum <= Minimum ? 1.0 : (Value - Minimum) / (Maximum - Minimum));

            if (IsIndeterminate)
                return;

            else
            {
                SetCurrentValue(IndicatorCompositorProperty, new Rect(0, ProgressRatio, 0, 0));
            }
        }

        private bool _runningAnimation = false;
        private void UpdateAnimation()
        {
            if (IsIndeterminate && IsVisible)
            {
                var spinAnimation = new RectAnimationUsingKeyFrames
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    Duration = TimeSpan.FromSeconds(ANIMATION_DURATION_BASIS * 2),
                    KeyFrames = new RectKeyFrameCollection
                    {
                        new DiscreteRectKeyFrame(new Rect(0, 0.1, 0, 1), KeyTime.FromPercent(0)),
                        new SpinnerKeyFrame(new Rect(0.1, 1.0,  90, 1), KeyTime.FromPercent(0.25), 1),
                        new SpinnerKeyFrame(new Rect(1.0, 1.1, 180, 1), KeyTime.FromPercent(0.50), 0),
                        new SpinnerKeyFrame(new Rect(1.1, 2.0, 270, 1), KeyTime.FromPercent(0.75), 1),
                        new SpinnerKeyFrame(new Rect(2.0, 2.1, 360, 1), KeyTime.FromPercent(1.00), 0),
                    },
                };
                spinAnimation.Freeze();
                BeginIndicatorAnimation(spinAnimation);

                _runningAnimation = true;
            }
            else if (_runningAnimation)
            {
                _runningAnimation = false;

                double rStart = (IndicatorCompositor.X + (IndicatorCompositor.Width / 360.0));
                double rEnd = (IndicatorCompositor.Y + (IndicatorCompositor.Width / 360.0));

                double sFactor = Math.Ceiling(rStart);
                rStart -= sFactor;
                rEnd -= sFactor;

                TimeSpan duration;
                if (Math.Abs(rStart) > Math.Abs(rEnd))
                    duration = TimeSpan.FromSeconds(ANIMATION_DURATION_BASIS * Math.Abs(rStart));
                else
                    duration = TimeSpan.FromSeconds(ANIMATION_DURATION_BASIS * Math.Abs(rEnd));

                var transitionAnimation = new RectAnimation
                {
                    Duration = duration,
                    From = new Rect(rStart, rEnd, 0, 1),
                    To = new Rect(0, ProgressRatio, 0, 0),
                };
                transitionAnimation.Freeze();
                BeginIndicatorAnimation(transitionAnimation);
                
            }
            else
            {
                UpdateIndicator();
            }
        }

        private void StopIndicatorAnimation()
        {
            if (null != _indicatorAnimationClock)
            {
                _indicatorAnimationClock.Controller.Stop();
                _indicatorAnimationClock.Controller.Remove();
            }
            ApplyAnimationClock(IndicatorCompositorProperty, null, HandoffBehavior.SnapshotAndReplace);
        }

        private void BeginIndicatorAnimation(AnimationTimeline animation, HandoffBehavior handoffBehavior = HandoffBehavior.SnapshotAndReplace)
        {
            if (null != _indicatorAnimationClock)
                StopIndicatorAnimation();

            AnimationClock clock = animation.CreateClock();
            clock.Controller.SpeedRatio = AnimationSpeedRatio;
            ApplyAnimationClock(IndicatorCompositorProperty, clock, handoffBehavior);

            _indicatorAnimationClock = clock;
        }

        private void BeginSmoothValueAnimation(double value)
        {
            if (IsIndeterminate)
            {
                SetCurrentValue(ValueProperty, value);
            }
            else
            {
                var valueAnimation = new DoubleAnimation
                {
                    To = value,
                    Duration = TimeSpan.FromMilliseconds(500),
                };
                BeginAnimation(ValueProperty, valueAnimation, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private static void ProgressStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProgressSpinner source = (ProgressSpinner)d;

            var newState = (ProgressState)e.NewValue;
            var oldState = (ProgressState)e.OldValue;

            // Set [IsIndeterminate] dependency property
            source.SetValue(IsIndeterminatePropertyKey, ProgressState.Indeterminate == newState);

            // Switch the required animation state according to changes in [ProgressState]
            source.UpdateAnimation();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            SetCurrentValue(MinHeightProperty, sizeInfo.NewSize.Width);
            UpdateIndicator();
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);
            
            if (newValue == 100.0)
            {
                SetCurrentValue(ProgressStateProperty, ProgressState.Completed);
            }

            UpdateIndicator();
        }

        private static Style CreateStyle()
        {
            var indicator = new FrameworkElementFactory(typeof(DrawingVisualHost), IndicatorTemplateName);

            var ellipse = new FrameworkElementFactory(typeof(Ellipse), "ellipse");
            ellipse.SetValue(Shape.StrokeThicknessProperty, new TemplateBindingExtension(CircleThicknessProperty));
            ellipse.SetValue(Shape.StrokeProperty, new TemplateBindingExtension(BackgroundProperty));
            ellipse.SetValue(WidthProperty, new TemplateBindingExtension(ActualWidthProperty));
            ellipse.SetValue(HeightProperty, new TemplateBindingExtension(ActualWidthProperty));

            var canvas = new FrameworkElementFactory(typeof(Canvas), TrackTemplateName);
            canvas.AppendChild(ellipse);
            canvas.AppendChild(indicator);

            var root = new FrameworkElementFactory(typeof(Grid), "TemplateRoot");
            root.AppendChild(canvas);

            var ct = new ControlTemplate(typeof(RangeBase)) { 
                VisualTree = root
            };

            var style = new Style(typeof(RangeBase));
            style.Setters.Add(new Setter(TemplateProperty, ct));
            return style;
        }

        // Create a host visual derived from the FrameworkElement class.
        // This class for using [DrawingVisual]
        private sealed class DrawingVisualHost : FrameworkElement
        {
            public static readonly DependencyProperty GeometryProperty =
                DependencyProperty.Register("Geometry", typeof(Geometry), typeof(DrawingVisualHost),
                    new FrameworkPropertyMetadata(new StreamGeometry()));

            public static readonly DependencyProperty PenProperty =
                DependencyProperty.Register("Pen", typeof(Pen), typeof(DrawingVisualHost),
                    new FrameworkPropertyMetadata(new Pen()));

            private DrawingVisual _drawing;

            public DrawingVisualHost()
            {
                _drawing = new DrawingVisual();
                AddVisualChild(_drawing);
            }

            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                using (DrawingContext context = _drawing.RenderOpen())
                {
                    context.DrawGeometry(null, (Pen)GetValue(PenProperty), (Geometry)GetValue(GeometryProperty));
                }
            }

            protected override int VisualChildrenCount
            {
                get { return 1; }
            }

            protected override Visual GetVisualChild(int index)
            {
                return _drawing;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var track = GetTemplateChild(TrackTemplateName);
            if (track != null)
            {
                _track = (FrameworkElement)track;
                _track.RenderTransformOrigin = new Point(0.5, 0.5);
                _track.RenderTransform = _scaleTransform;
            }

            // Apply some bindings to [PART_Indicator]
            var indicator = GetTemplateChild(IndicatorTemplateName);
            if (indicator != null)
            {
                _indicator = (FrameworkElement)indicator;

                var indicatorBinding = new MultiBinding
                {
                    Converter = IndicatorConverter.I
                };
                indicatorBinding.Bindings.Add(new Binding("ActualWidth") { Source = this, Mode = BindingMode.OneWay });
                indicatorBinding.Bindings.Add(new Binding("CircleThickness") { Source = this, Mode = BindingMode.OneWay });
                indicatorBinding.Bindings.Add(new Binding("ProgressRatio") { Source = this, Mode = BindingMode.OneWay });
                indicatorBinding.Bindings.Add(new Binding("IndicatorCompositor") { Source = this, Mode = BindingMode.OneWay });

                var indicatorPen = new MultiBinding
                {
                    Converter = PenConverter.I
                };
                indicatorPen.Bindings.Add(new Binding("CircleThickness") { Source = this, Mode = BindingMode.OneWay });
                indicatorPen.Bindings.Add(new Binding("Foreground") { Source = this, Mode = BindingMode.OneWay });

                _indicator.InvalidateProperty(DrawingVisualHost.GeometryProperty);
                _indicator.SetBinding(DrawingVisualHost.GeometryProperty, indicatorBinding);

                _indicator.InvalidateProperty(DrawingVisualHost.PenProperty);
                _indicator.SetBinding(DrawingVisualHost.PenProperty, indicatorPen);
            }
        }

        #region Dependency properties

        public ProgressState ProgressState
        {
            get { return (ProgressState)GetValue(ProgressStateProperty); }
            set { SetValue(ProgressStateProperty, value); }
        }
        public static readonly DependencyProperty ProgressStateProperty =
            DependencyProperty.Register("ProgressState",
                typeof(ProgressState), typeof(ProgressSpinner), new FrameworkPropertyMetadata(
                    _defaultState, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                        new PropertyChangedCallback(ProgressStateChanged)));

        public bool IsIndeterminate
        {
            get { return (bool)GetValue(IsIndeterminatePropertyKey.DependencyProperty); }
        }

        private static readonly DependencyPropertyKey IsIndeterminatePropertyKey =
            DependencyProperty.RegisterReadOnly("IsIndeterminate", typeof(bool),
                    typeof(ProgressSpinner), new FrameworkPropertyMetadata(_defaultState == ProgressState.Indeterminate));

        public double AnimationSpeedRatio
        {
            get { return (double)GetValue(AnimationSpeedRatioProperty); }
            set { SetValue(ProgressStateProperty, value); }
        }

        public static readonly DependencyProperty AnimationSpeedRatioProperty =
            DependencyProperty.Register("AnimationSpeedRatio",
                typeof(double), typeof(ProgressSpinner), new FrameworkPropertyMetadata(
                    1.0, 
                    propertyChangedCallback: (d, e) =>
                    {
                        var source = (ProgressSpinner)d;

                        if (source._indicatorAnimationClock != null)
                            source._indicatorAnimationClock.Controller.SpeedRatio = (double)e.NewValue;
                    },
                    coerceValueCallback: (d, baseValue) =>
                    {
                        if ((double)baseValue > 0.1)
                            return baseValue;
                        else
                            return 0.1;
                    }));

        public double CircleScale
        {
            get { return (double)GetValue(CircleScaleProperty); }
            set { SetValue(CircleScaleProperty, value); }
        }

        public static readonly DependencyProperty CircleScaleProperty =
            DependencyProperty.Register("CircleScale",
                typeof(double), typeof(ProgressSpinner), new FrameworkPropertyMetadata(
                    1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, 
                    propertyChangedCallback: (d, e) =>
                    {
                        var source = (ProgressSpinner)d;
                        source._scaleTransform.ScaleX = (double)e.NewValue;
                        source._scaleTransform.ScaleY = (double)e.NewValue;
                    }, 
                    coerceValueCallback: (d, baseValue) =>
                    {
                        if ((double)baseValue > 0.0)
                            return baseValue;
                        else
                            return 0.0;
                    }
                )
            );

        public double CircleThickness
        {
            get { return (double)GetValue(CircleThicknessProperty); }
            set { SetValue(CircleThicknessProperty, value); }
        }

        public static readonly DependencyProperty CircleThicknessProperty =
            DependencyProperty.Register("CircleThickness", typeof(double), typeof(ProgressSpinner),
                new FrameworkPropertyMetadata(5.0,
                    propertyChangedCallback:  (d, e) =>
                    {
                        ((ProgressSpinner)d).UpdateIndicator();
                    },
                    coerceValueCallback: (d, baseValue) =>
                    {
                        if ((double)baseValue > 0.0)
                            return baseValue;
                        else
                            return 0.0;
                    }
                )
            );

        public double SmoothValue
        {
            get { return (double)GetValue(SmoothValueProperty); }
            set { SetValue(SmoothValueProperty, value); }
        }

        public static readonly DependencyProperty SmoothValueProperty =
            DependencyProperty.Register("SmoothValue",
                typeof(double), typeof(ProgressSpinner), new PropertyMetadata(
                    (d, e) => { ((ProgressSpinner)d).BeginSmoothValueAnimation((double)e.NewValue); }));

        /*** Private dependency properties ***/

        private double ProgressRatio
{
            get { return (double)GetValue(ProgressRatioProperty); }
            set { SetValue(ProgressRatioProperty, value); }
        }

        private static readonly DependencyProperty ProgressRatioProperty =
            DependencyProperty.Register("ProgressRatio", typeof(double), typeof(ProgressSpinner));

        private Rect IndicatorCompositor
        {
            get { return (Rect)GetValue(IndicatorCompositorProperty); }
            set { SetValue(IndicatorCompositorProperty, value); }
        }

        private static readonly DependencyProperty IndicatorCompositorProperty =
            DependencyProperty.Register("IndicatorCompositor", typeof(Rect), typeof(ProgressSpinner));

        #endregion Dependency properties

        #region Converters

        private sealed class IndicatorConverter : IMultiValueConverter
        {
            public static IndicatorConverter I = new IndicatorConverter();
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                double actualWidth = (double)values[0];
                double strokeThickness = (double)values[1];
                double progressRatio = (double)values[2];
                double ratio0 = ((Rect)values[3]).X;
                double ratio1 = ((Rect)values[3]).Y;
                double rotate = ((Rect)values[3]).Width;
                double ignoreProgress = ((Rect)values[3]).Height;

                if (ignoreProgress < 0)
                    ignoreProgress = 0;
                else if (ignoreProgress > 1)
                    ignoreProgress = 1;

                double startAngleRatio;
                double endAngleRatio;
                if (ratio0 < ratio1)
                {
                    startAngleRatio = ratio0 * ignoreProgress;
                    endAngleRatio = (ratio1 * ignoreProgress) + (progressRatio * (1 - ignoreProgress));
                }
                else
                {
                    startAngleRatio = ratio1 * ignoreProgress;
                    endAngleRatio = (ratio0 * ignoreProgress) + (progressRatio * (1 - ignoreProgress));
                }

                double radius;
                if (actualWidth > strokeThickness)
                {
                    radius = (actualWidth - strokeThickness) * 0.5;
                }
                else
                {
                    radius = 0.0;
                }
                Size size = new Size(radius, radius);
                double positionOffset = actualWidth / 2;

                double offsetRadians = rotate / 360 * Math.PI * 2;
                double startRadians = startAngleRatio * Math.PI * 2 + offsetRadians;
                double endRadians = endAngleRatio * Math.PI * 2 + offsetRadians;

                int midpointCount = (int)((endRadians - startRadians) / (0.5 * Math.PI * 2)) + 1;
                double midpointRadians = (endRadians - startRadians) / midpointCount;

                double startX = radius * Math.Sin(startRadians) + positionOffset;
                double startY = -radius * Math.Cos(startRadians) + positionOffset;

                double endX = radius * Math.Sin(endRadians) + positionOffset;
                double endY = -radius * Math.Cos(endRadians) + positionOffset;

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(startX, startY), false, false);
                    for (int i = 1; i < midpointCount; i++)
                    {
                        double midX = radius * Math.Sin(midpointRadians * i + startRadians) + positionOffset;
                        double midY = -radius * Math.Cos(midpointRadians * i + startRadians) + positionOffset;
                        ctx.ArcTo(new Point(midX, midY), size, 0.0, false, SweepDirection.Clockwise, true, false);
                    }
                    ctx.ArcTo(new Point(endX, endY), size, 0.0, false, SweepDirection.Clockwise, true, false);
                }
                geometry.Freeze();
                return geometry;
            }

            public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }


        private sealed class PenConverter : IMultiValueConverter
        {
            public static PenConverter I = new PenConverter();
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                double strokeThickness = (double)values[0];
                Brush brush = (Brush)values[1];
                var pen = new Pen
                {
                    Brush = brush,
                    Thickness = strokeThickness,
                    EndLineCap = PenLineCap.Round,
                    StartLineCap = PenLineCap.Round,
                };
                pen.Freeze();
                return pen;
            }

            public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        #endregion Converters

        private class SpinnerKeyFrame : RectKeyFrame
        {
            public int TargetPosition = -1;

            public SpinnerKeyFrame(Rect value, KeyTime keyTime, int targetPosition)
                : base(value, keyTime)
            {
                TargetPosition = targetPosition;
            }

            private double InterpolateDouble(double from, double to, double progress) => from + ((to - from) * progress);

            protected override Rect InterpolateValueCore(Rect baseValue, double keyFrameProgress)
            {
                if (keyFrameProgress == 0.0)
                {
                    return baseValue;
                }
                else if (keyFrameProgress == 1.0)
                {
                    return Value;
                }
                else
                {
                    double easingProgress;
                    if (keyFrameProgress < 0.5)
                    {
                        var x = (keyFrameProgress * 2);
                        easingProgress = x * x / 2;
                    }
                    else
                    {
                        var x = (keyFrameProgress * 2 - 2);
                        easingProgress = -1 * (x * x) / 2 + 1;
                    }

                    switch (TargetPosition)
                    {
                        case 0:
                            return new Rect(
                                InterpolateDouble(baseValue.X, Value.X, easingProgress),
                                InterpolateDouble(baseValue.Y, Value.Y, keyFrameProgress),
                                InterpolateDouble(baseValue.Width, Value.Width, keyFrameProgress),
                                InterpolateDouble(baseValue.Height, Value.Height, keyFrameProgress)
                        );
                        case 1:
                            return new Rect(
                                InterpolateDouble(baseValue.X, Value.X, keyFrameProgress),
                                InterpolateDouble(baseValue.Y, Value.Y, easingProgress),
                                InterpolateDouble(baseValue.Width, Value.Width, keyFrameProgress),
                                InterpolateDouble(baseValue.Height, Value.Height, keyFrameProgress)
                            );
                        case 2:
                            return new Rect(
                                InterpolateDouble(baseValue.X, Value.X, easingProgress),
                                InterpolateDouble(baseValue.Y, Value.Y, easingProgress),
                                InterpolateDouble(baseValue.Width, Value.Width, keyFrameProgress),
                                InterpolateDouble(baseValue.Height, Value.Height, keyFrameProgress)
                            );
                        default:
                            return new Rect(
                                InterpolateDouble(baseValue.X, Value.X, keyFrameProgress),
                                InterpolateDouble(baseValue.Y, Value.Y, keyFrameProgress),
                                InterpolateDouble(baseValue.Width, Value.Width, keyFrameProgress),
                                InterpolateDouble(baseValue.Height, Value.Height, keyFrameProgress)
                            );
                    }
                }
            }

            protected override Freezable CreateInstanceCore()
            {
                return new SpinnerKeyFrame(Value, KeyTime, TargetPosition);
            }
        }
    }
}
