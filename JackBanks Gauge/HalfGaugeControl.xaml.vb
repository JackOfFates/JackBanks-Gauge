Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.ComponentModel
Imports System.Windows.Data
Imports System.Windows.Media.Effects
Imports System.Windows.Shapes
Imports System.Windows.Threading

Partial Public Class HalfGaugeControl
    Inherits UserControl

    Public Shared ReadOnly MinimumProperty As DependencyProperty = DependencyProperty.Register("Minimum", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(0.0, AddressOf OnLimitsChanged))
    Public Shared ReadOnly MaximumProperty As DependencyProperty = DependencyProperty.Register("Maximum", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(7000.0, AddressOf OnLimitsChanged))
    Public Shared ReadOnly ValueProperty As DependencyProperty = DependencyProperty.Register("Value", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(0.0, AddressOf OnValueChanged))
    Public Shared ReadOnly TitleProperty As DependencyProperty = DependencyProperty.Register("Title", GetType(String), GetType(HalfGaugeControl), New PropertyMetadata("RPM", AddressOf OnTitleChanged))
    Public Shared ReadOnly UnitsProperty As DependencyProperty = DependencyProperty.Register("Units", GetType(String), GetType(HalfGaugeControl), New PropertyMetadata("x1000", AddressOf OnUnitsChanged))
    Public Shared ReadOnly ColorProperty As DependencyProperty = DependencyProperty.Register("Color", GetType(Brush), GetType(HalfGaugeControl), New PropertyMetadata(Brushes.Red, AddressOf OnColorChanged))
    Public Shared ReadOnly RedlineProperty As DependencyProperty = DependencyProperty.Register("Redline", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(70.0, AddressOf OnRedlineChanged))
    Public Shared ReadOnly RedLineColorProperty As DependencyProperty = DependencyProperty.Register("RedLineColor", GetType(Brush), GetType(HalfGaugeControl), New PropertyMetadata(Brushes.Red, AddressOf OnRedLineColorChanged))
    Public Shared ReadOnly TrailEnabledProperty As DependencyProperty = DependencyProperty.Register("TrailEnabled", GetType(Boolean), GetType(HalfGaugeControl), New PropertyMetadata(True, AddressOf OnTrailEnabledChanged))
    Public Shared ReadOnly TrailSegmentsProperty As DependencyProperty = DependencyProperty.Register("TrailSegments", GetType(Integer), GetType(HalfGaugeControl), New PropertyMetadata(128, AddressOf OnTrailSegmentsChanged))
    Public Shared ReadOnly TrailDynamicsProperty As DependencyProperty = DependencyProperty.Register("TrailDynamics", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(50.0))
    Public Shared ReadOnly DetectedMinProperty As DependencyProperty = DependencyProperty.Register("DetectedMin", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(Double.NaN))
    Public Shared ReadOnly DetectedMaxProperty As DependencyProperty = DependencyProperty.Register("DetectedMax", GetType(Double), GetType(HalfGaugeControl), New PropertyMetadata(Double.NaN))

    Private Const StartAngle As Double = -90
    Private Const EndAngle As Double = 90

    Private _lastValue As Double = 0
    Private _lastUpdateTime As DateTime = DateTime.Now
    Private _velocity As Double = 0
    Private _blurMagnitude As Double = 0
    Private _updateTimestamps As New System.Collections.Generic.List(Of DateTime)()
    Private _decayTimer As DispatcherTimer
    Private _isAnimating As Boolean = False
    Private _needleDirty As Boolean = False
    Private Const VelocityDecayRate As Double = 0.92
    Private Const UpdateWindowSeconds As Double = 1.0
    Private Const MaxUpdatesPerSecond As Double = 10.0

    Private Const TrailMaxOpacity As Double = 0.6
    Private _trailArcs As New List(Of Path)()
    Private _trailFigures As New List(Of PathFigure)()
    Private _trailLines As New List(Of LineSegment)()
    Private _trailArcSegs As New List(Of ArcSegment)()
    Private _trailAnchorAngle As Double = Double.NaN
    Private Const TrailRadius As Double = 108.0
    Private Const TrailAnchorDecay As Double = 0.048

    Private _blurAnglePropInfo As System.Reflection.PropertyInfo
    Private _blurMagnitudePropInfo As System.Reflection.PropertyInfo
    Private _effectPropsCached As Boolean = False

    Public Sub New()
        InitializeComponent()
        _decayTimer = New DispatcherTimer() With {
            .Interval = TimeSpan.FromMilliseconds(16)
        }
        AddHandler _decayTimer.Tick, AddressOf DecayTimer_Tick
        AddHandler Me.Loaded, AddressOf HalfGaugeControl_Loaded
    End Sub

    Private Sub StartAnimation()
        If Not _isAnimating Then
            _isAnimating = True
            _decayTimer.Start()
        End If
    End Sub

    Private Sub StopAnimation()
        If _isAnimating Then
            _isAnimating = False
            _decayTimer.Stop()
        End If
    End Sub

    Private Sub HalfGaugeControl_Loaded(sender As Object, e As RoutedEventArgs)
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            CreateTrailElements()
            UpdateNeedle()
            UpdateRedlineVisual()
            TitleText.Text = Title
            UnitsText.Text = Units
            ValueText.Text = Value.ToString()
            UpdateTickLabels()
            Try
                If ValueText IsNot Nothing Then
                    ValueText.Foreground = Me.Color
                End If
            Catch
            End Try
        End If
    End Sub

    <Category("Gauge")>
    Public Property Redline As Double
        Get
            Return CDbl(GetValue(RedlineProperty))
        End Get
        Set(value As Double)
            SetValue(RedlineProperty, value)
        End Set
    End Property

    <Category("Gauge")>
    Public Property RedLineColor As Brush
        Get
            Return CType(GetValue(RedLineColorProperty), Brush)
        End Get
        Set(v As Brush)
            SetValue(RedLineColorProperty, v)
        End Set
    End Property

    Private Shared Sub OnRedlineChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing Then
            g.Dispatcher.BeginInvoke(Sub()
                                         Try
                                             g.UpdateRedlineVisual()
                                         Catch
                                         End Try
                                     End Sub)
        End If
    End Sub

    Private Sub UpdateRedlineVisual()
        Try
            Dim pct = Math.Min(100.0, Math.Max(0.0, Me.Redline))
            Dim t = pct / 100.0
            Dim newRadiusY = 0.0 + t * 100.0
            Dim newAngle = -70.0 + t * 75.0
            If Me.RedlineGeometry IsNot Nothing Then
                Me.RedlineGeometry.RadiusY = newRadiusY
            End If
            If Me.RedlineOffsetAngle IsNot Nothing Then
                Me.RedlineOffsetAngle.Angle = newAngle
            End If
        Catch
        End Try
    End Sub

    Private Sub CacheEffectProperties()
        If _effectPropsCached Then Return
        Dim needle = Me.Needle
        If needle IsNot Nothing AndAlso needle.Effect IsNot Nothing Then
            Dim effectType = needle.Effect.GetType()
            _blurAnglePropInfo = effectType.GetProperty("BlurAngle")
            _blurMagnitudePropInfo = effectType.GetProperty("BlurMagnitude")
            _effectPropsCached = True
        End If
    End Sub

    Private Sub DecayTimer_Tick(sender As Object, e As EventArgs)
        Try
            ' Process coalesced needle update
            If _needleDirty Then
                _needleDirty = False
                UpdateNeedle()
            End If

            ' Decay velocity each frame
            _velocity *= VelocityDecayRate

            ' Calculate target blur from current velocity
            Dim targetBlur = Math.Min(50.0, Math.Abs(_velocity) / 1000.0 * 50.0)
            _blurMagnitude += (targetBlur - _blurMagnitude) * 0.1

            Dim blurSettled As Boolean = False
            If Math.Abs(_blurMagnitude) < 0.001 AndAlso targetBlur < 0.001 Then
                _blurMagnitude = 0
                blurSettled = True
            End If

            ' Apply blur effect using cached reflection
            CacheEffectProperties()
            Dim needle = Me.Needle
            If needle IsNot Nothing AndAlso needle.Effect IsNot Nothing Then
                Try
                    If _blurAnglePropInfo IsNot Nothing Then
                        _blurAnglePropInfo.SetValue(needle.Effect, 0.0)
                    End If
                    If _blurMagnitudePropInfo IsNot Nothing Then
                        _blurMagnitudePropInfo.SetValue(needle.Effect, -_blurMagnitude)
                    End If
                Catch
                End Try
            End If

            ' Animate needle trail
            Try
                UpdateTrail()
            Catch
            End Try

            ' Check if trail has settled
            Dim trailSettled As Boolean = False
            If Not Double.IsNaN(_trailAnchorAngle) Then
                Dim currentAngle = NeedleRotate.Angle
                trailSettled = Math.Abs(currentAngle - _trailAnchorAngle) < 0.1
            Else
                trailSettled = True
            End If

            ' Stop timer when all animations have settled
            If blurSettled AndAlso trailSettled Then
                StopAnimation()
            End If
        Catch : End Try
    End Sub

    <Category("Gauge")>
    Public Property TrailEnabled As Boolean
        Get
            Return CBool(GetValue(TrailEnabledProperty))
        End Get
        Set(v As Boolean)
            SetValue(TrailEnabledProperty, v)
        End Set
    End Property

    <Category("Gauge")>
    Public Property TrailSegments As Integer
        Get
            Return CInt(GetValue(TrailSegmentsProperty))
        End Get
        Set(v As Integer)
            SetValue(TrailSegmentsProperty, v)
        End Set
    End Property

    <Category("Gauge")>
    Public Property TrailDynamics As Double
        Get
            Return CDbl(GetValue(TrailDynamicsProperty))
        End Get
        Set(v As Double)
            SetValue(TrailDynamicsProperty, Math.Min(100.0, Math.Max(0.0, v)))
        End Set
    End Property

    <Category("Gauge")>
    Public Property DetectedMin As Double
        Get
            Return CDbl(GetValue(DetectedMinProperty))
        End Get
        Set(v As Double)
            SetValue(DetectedMinProperty, v)
        End Set
    End Property

    <Category("Gauge")>
    Public Property DetectedMax As Double
        Get
            Return CDbl(GetValue(DetectedMaxProperty))
        End Get
        Set(v As Double)
            SetValue(DetectedMaxProperty, v)
        End Set
    End Property

    Public Sub ResetDetectedRange()
        DetectedMin = Double.NaN
        DetectedMax = Double.NaN
        Try
            If MinDetectedText IsNot Nothing Then MinDetectedText.Text = ""
            If MaxDetectedText IsNot Nothing Then MaxDetectedText.Text = ""
        Catch
        End Try
    End Sub

    Private Shared Sub OnTrailEnabledChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing AndAlso g.IsLoaded Then
            If Not CBool(e.NewValue) Then
                For Each p In g._trailArcs
                    p.Opacity = 0
                Next
                g._trailAnchorAngle = Double.NaN
            End If
        End If
    End Sub

    Private Shared Sub OnTrailSegmentsChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing AndAlso g.IsLoaded Then
            g.RebuildTrailElements()
        End If
    End Sub

    <Category("Gauge")>
    Public Property Minimum As Double
        Get
            Return CDbl(GetValue(MinimumProperty))
        End Get
        Set(value As Double)
            SetValue(MinimumProperty, value)
        End Set
    End Property

    <Category("Gauge")>
    Public Property Maximum As Double
        Get
            Return CDbl(GetValue(MaximumProperty))
        End Get
        Set(value As Double)
            SetValue(MaximumProperty, value)
        End Set
    End Property

    <Category("Gauge")>
    Public Property Value As Double
        Get
            Return CDbl(GetValue(ValueProperty))
        End Get
        Set(v As Double)
            SetValue(ValueProperty, v)
        End Set
    End Property

    <Category("Gauge")>
    Public Property Title As String
        Get
            Return CStr(GetValue(TitleProperty))
        End Get
        Set(v As String)
            SetValue(TitleProperty, v)
        End Set
    End Property

    <Category("Gauge")>
    Public Property Units As String
        Get
            Return CStr(GetValue(UnitsProperty))
        End Get
        Set(v As String)
            SetValue(UnitsProperty, v)
        End Set
    End Property

    <Category("Gauge")>
    Public Property [Color] As Brush
        Get
            Return CType(GetValue(ColorProperty), Brush)
        End Get
        Set(v As Brush)
            SetValue(ColorProperty, v)
        End Set
    End Property

    Private Shared Sub OnLimitsChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            Try
                g.UpdateTickLabels()
                g.UpdateNeedle()
            Catch
            End Try
        End If
    End Sub

    Private Sub UpdateTickLabels()
        Try
            Dim range = Maximum - Minimum
            Dim labels = New TextBlock() {TickLabel0, TickLabel1, TickLabel2, TickLabel3, TickLabel4, TickLabel5, TickLabel6}
            Dim steps = labels.Length - 1
            If steps <= 0 Then Return
            Dim [step] As Double = range / steps

            For i = 0 To labels.Length - 1
                Dim tb = labels(i)
                If tb IsNot Nothing Then
                    Dim val As Double = Minimum + [step] * i
                    If i = 0 Then val = Minimum
                    If i = labels.Length - 1 Then val = Maximum
                    Dim rounded = Math.Round(val / 100.0) * 100.0
                    tb.Text = CInt(rounded).ToString()
                End If
            Next
        Catch
        End Try
    End Sub

    Private Shared Sub OnValueChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            g._needleDirty = True
            g.StartAnimation()
        End If
    End Sub

    Private Shared Sub OnTitleChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            Try
                g.TitleText.Text = CStr(e.NewValue)
            Catch
            End Try
        End If
    End Sub

    Private Shared Sub OnUnitsChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            Try
                g.UnitsText.Text = CStr(e.NewValue)
            Catch
            End Try
        End If
    End Sub

    Private Shared Sub OnColorChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing Then
            g.Dispatcher.BeginInvoke(Sub()
                                         Try
                                             If g.ValueText IsNot Nothing Then
                                                 Dim b = TryCast(e.NewValue, Brush)
                                                 If b IsNot Nothing Then
                                                     g.ValueText.Foreground = b
                                                 End If
                                             End If
                                         Catch
                                         End Try
                                     End Sub)
        End If
    End Sub

    Private Shared Sub OnRedLineColorChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, HalfGaugeControl)
        If g IsNot Nothing Then
            g.Dispatcher.BeginInvoke(Sub()
                                         Try
                                             Dim b = TryCast(e.NewValue, Brush)
                                             If b IsNot Nothing Then
                                                 If g.TickCircle IsNot Nothing Then
                                                     g.TickCircle.Stroke = b
                                                 End If
                                             End If
                                         Catch
                                         End Try
                                     End Sub)
        End If
    End Sub

    Private Sub CreateTrailElements()
        RebuildTrailElements()
    End Sub

    Private Sub RebuildTrailElements()
        For Each p In _trailArcs
            NeedleCanvas.Children.Remove(p)
        Next
        _trailArcs.Clear()
        _trailFigures.Clear()
        _trailLines.Clear()
        _trailArcSegs.Clear()
        _trailAnchorAngle = Double.NaN

        Dim count = Math.Max(1, TrailSegments)

        For i = 0 To count - 1
            Dim line As New LineSegment(New Point(150, 150), True)
            Dim arc As New ArcSegment() With {
                .Point = New Point(150, 150),
                .SweepDirection = SweepDirection.Clockwise,
                .Size = New Size(TrailRadius, TrailRadius),
                .IsLargeArc = False,
                .IsStroked = True
            }
            Dim fig As New PathFigure() With {
                .StartPoint = New Point(150, 150),
                .IsClosed = True,
                .IsFilled = True
            }
            fig.Segments.Add(line)
            fig.Segments.Add(arc)

            Dim geom As New PathGeometry()
            geom.Figures.Add(fig)

            Dim sectorPath As New Path() With {
                .StrokeThickness = 0,
                .Opacity = 0,
                .IsHitTestVisible = False,
                .Data = geom
            }
            sectorPath.SetBinding(Shape.FillProperty,
                New Binding("Foreground") With {.Source = ValueText})

            NeedleCanvas.Children.Insert(0, sectorPath)
            _trailArcs.Add(sectorPath)
            _trailFigures.Add(fig)
            _trailLines.Add(line)
            _trailArcSegs.Add(arc)
        Next
    End Sub

    Private Sub UpdateTrail(Optional applyDecay As Boolean = True)
        If _trailArcs.Count = 0 OrElse Not TrailEnabled Then Return

        Dim currentAngle = NeedleRotate.Angle
        Dim count = _trailArcs.Count

        If Double.IsNaN(_trailAnchorAngle) Then
            _trailAnchorAngle = currentAngle
        End If

        Dim sweep = currentAngle - _trailAnchorAngle
        Dim absSweep = Math.Abs(sweep)

        If absSweep < 0.5 Then
            For i = 0 To count - 1
                _trailArcs(i).Opacity = 0
            Next
            If applyDecay Then
                _trailAnchorAngle += (currentAngle - _trailAnchorAngle) * 0.15
                If Math.Abs(currentAngle - _trailAnchorAngle) < 0.1 Then
                    _trailAnchorAngle = currentAngle
                End If
            End If
            Return
        End If

        Dim lowAngle = Math.Min(currentAngle, _trailAnchorAngle)
        Dim highAngle = Math.Max(currentAngle, _trailAnchorAngle)
        Dim sliceAngle = absSweep / count
        Dim arcSize = New Size(TrailRadius, TrailRadius)
        Dim overlap = 0.15

        For i = 0 To count - 1
            Dim segStart = lowAngle + i * sliceAngle
            Dim segEnd = segStart + sliceAngle + overlap

            _trailLines(i).Point = TrailArcPoint(segStart)
            _trailArcSegs(i).Point = TrailArcPoint(segEnd)
            _trailArcSegs(i).Size = arcSize
            _trailArcSegs(i).IsLargeArc = (segEnd - segStart) > 180.0

            Dim t As Double
            If sweep > 0 Then
                t = (i + 1.0) / count
            Else
                t = 1.0 - (CDbl(i) / count)
            End If
            _trailArcs(i).Opacity = TrailMaxOpacity * t
        Next

        If applyDecay Then
            Dim dynamicsFactor = Math.Min(100.0, Math.Max(0.0, TrailDynamics)) / 100.0
            Dim normalizedVelocity = Math.Min(1.0, Math.Abs(_velocity) / 1000.0)
            Dim effectiveDecay = TrailAnchorDecay + dynamicsFactor * normalizedVelocity * 0.2
            _trailAnchorAngle += (currentAngle - _trailAnchorAngle) * effectiveDecay
        End If
    End Sub

    Private Function TrailArcPoint(angle As Double) As Point
        Dim rad = angle * Math.PI / 180.0
        Return New Point(150.0 + TrailRadius * Math.Sin(rad), 150.0 - TrailRadius * Math.Cos(rad))
    End Function

    Private Sub UpdateRgbEffectAmount()
    End Sub

    Private Sub UpdateNeedle()
        Dim valForNeedle = Value
        If valForNeedle < Minimum Then valForNeedle = Minimum
        If valForNeedle > Maximum Then valForNeedle = Maximum

        Dim angle = StartAngle + (valForNeedle - Minimum) / (Maximum - Minimum) * (EndAngle - StartAngle)
        NeedleRotate.Angle = angle
        ValueText.Text = Value.ToString("0")

        ' Track detected min/max
        If Double.IsNaN(DetectedMin) OrElse Value < DetectedMin Then
            DetectedMin = Value
        End If
        If Double.IsNaN(DetectedMax) OrElse Value > DetectedMax Then
            DetectedMax = Value
        End If
        Try
            If MinDetectedText IsNot Nothing Then
                MinDetectedText.Text = If(Double.IsNaN(DetectedMin), "", DetectedMin.ToString("0"))
            End If
            If MaxDetectedText IsNot Nothing Then
                MaxDetectedText.Text = If(Double.IsNaN(DetectedMax), "", DetectedMax.ToString("0"))
            End If
        Catch
        End Try

        ' Calculate velocity from value change rate
        Dim currentTime = DateTime.Now
        Dim timeDelta = (currentTime - _lastUpdateTime).TotalSeconds
        If timeDelta > 0 Then
            _velocity = (Value - _lastValue) / timeDelta
        End If
        _lastValue = Value
        _lastUpdateTime = currentTime

        ' Blur and trail updates are handled by the timer at ~60fps
        StartAnimation()
    End Sub

End Class
