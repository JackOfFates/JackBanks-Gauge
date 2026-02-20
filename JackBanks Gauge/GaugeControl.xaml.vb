Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.ComponentModel
Imports System.Windows.Media.Effects
Imports System.Windows.Threading

Partial Public Class GaugeControl
    Inherits UserControl

    Public Shared ReadOnly MinimumProperty As DependencyProperty = DependencyProperty.Register("Minimum", GetType(Double), GetType(GaugeControl), New PropertyMetadata(0.0, AddressOf OnLimitsChanged))
    Public Shared ReadOnly MaximumProperty As DependencyProperty = DependencyProperty.Register("Maximum", GetType(Double), GetType(GaugeControl), New PropertyMetadata(7000.0, AddressOf OnLimitsChanged))
    Public Shared ReadOnly ValueProperty As DependencyProperty = DependencyProperty.Register("Value", GetType(Double), GetType(GaugeControl), New PropertyMetadata(0.0, AddressOf OnValueChanged))
    Public Shared ReadOnly TitleProperty As DependencyProperty = DependencyProperty.Register("Title", GetType(String), GetType(GaugeControl), New PropertyMetadata("RPM", AddressOf OnTitleChanged))
    Public Shared ReadOnly UnitsProperty As DependencyProperty = DependencyProperty.Register("Units", GetType(String), GetType(GaugeControl), New PropertyMetadata("x1000", AddressOf OnUnitsChanged))
    Public Shared ReadOnly ColorProperty As DependencyProperty = DependencyProperty.Register("Color", GetType(Brush), GetType(GaugeControl), New PropertyMetadata(Brushes.Red, AddressOf OnColorChanged))
    Public Shared ReadOnly RedlineProperty As DependencyProperty = DependencyProperty.Register("Redline", GetType(Double), GetType(GaugeControl), New PropertyMetadata(70.0, AddressOf OnRedlineChanged))
    Public Shared ReadOnly RedLineColorProperty As DependencyProperty = DependencyProperty.Register("RedLineColor", GetType(Brush), GetType(GaugeControl), New PropertyMetadata(Brushes.Red, AddressOf OnRedLineColorChanged))

    Private Const StartAngle As Double = -120
    Private Const EndAngle As Double = 120
    Private Const NeedleCalibrationNotUsed As Boolean = False

    Private _lastValue As Double = 0
    Private _lastUpdateTime As DateTime = DateTime.Now
    Private _velocity As Double = 0
    Private _blurMagnitude As Double = 0
    Private _updateTimestamps As New System.Collections.Generic.List(Of DateTime)()
    Private _decayTimer As DispatcherTimer
    Private Const VelocityDecayRate As Double = 0.92
    Private Const BlurAngleOffset As Double = 0
    Private Const UpdateWindowSeconds As Double = 1.0
    Private Const MaxUpdatesPerSecond As Double = 10.0

    Public Sub New()
        InitializeComponent()
        ' Timer to decay blur when control is not being updated
        _decayTimer = New DispatcherTimer() With {
            .Interval = TimeSpan.FromMilliseconds(1)
        }
        AddHandler _decayTimer.Tick, AddressOf DecayTimer_Tick
        _decayTimer.Start()
        AddHandler Me.Loaded, AddressOf GaugeControl_Loaded
    End Sub

    Private Sub GaugeControl_Loaded(sender As Object, e As RoutedEventArgs)
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            RenderTicks()
            UpdateNeedle()
            ' Apply any configured redline value to the visual elements
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
        Dim g = TryCast(d, GaugeControl)
        If g IsNot Nothing Then
            ' Ensure update happens on UI thread
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

            ' Interpolate RadiusY from 0 to 200 and Angle from -50 to 0
            Dim newRadiusY = 0.0 + t * 100.0
            Dim newAngle = -50.0 + t * 75.0

            If Me.RedlineGeometry IsNot Nothing Then
                Me.RedlineGeometry.RadiusY = newRadiusY
            End If

            If Me.RedlineOffsetAngle IsNot Nothing Then
                Me.RedlineOffsetAngle.Angle = newAngle
            End If
        Catch
        End Try
    End Sub

    Private Sub DecayTimer_Tick(sender As Object, e As EventArgs)
        Try
            ' Decay blur magnitude smoothly toward zero when no recent updates
            If Math.Abs(_blurMagnitude) > 0.001 Then
                _blurMagnitude += (0 - _blurMagnitude) * 0.1
            Else
                _blurMagnitude = 0
            End If

            ' Apply the decayed blur magnitude to the effect
            Dim canvas = NeedleCanvas
            If canvas IsNot Nothing AndAlso canvas.Effect IsNot Nothing Then
                Try
                    Dim effectType = canvas.Effect.GetType()
                    Dim blurMagnitudeProperty = effectType.GetProperty("BlurMagnitude")
                    If blurMagnitudeProperty IsNot Nothing Then
                        blurMagnitudeProperty.SetValue(canvas.Effect, -_blurMagnitude)
                    End If
                Catch
                End Try
            End If
            ' Update RGB chromatic aberration effect amount based on blur
            Try
                UpdateRgbEffectAmount()
            Catch
            End Try
        Catch : End Try
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
        Dim g = TryCast(d, GaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            g.RenderTicks()
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
            Dim labels = New TextBlock() {TickLabel0, TickLabel1, TickLabel2, TickLabel3, TickLabel4, TickLabel5, TickLabel6, TickLabel7, TickLabel8}
            Dim steps = labels.Length - 1
            If steps <= 0 Then Return
            Dim [step] As Double = range / steps

            For i = 0 To labels.Length - 1
                Dim tb = labels(i)
                If tb IsNot Nothing Then
                    Dim val As Double = Minimum + [step] * i
                    ' Ensure first and last are exact Minimum/Maximum
                    If i = 0 Then val = Minimum
                    If i = labels.Length - 1 Then val = Maximum

                    ' Round to nearest 100
                    Dim rounded = Math.Round(val / 100.0) * 100.0
                    tb.Text = CInt(rounded).ToString()
                End If
            Next
        Catch
        End Try
    End Sub

    Private Shared Sub OnValueChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, GaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            g.UpdateNeedle()
        End If
    End Sub

    Private Shared Sub OnTitleChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, GaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            Try
                g.TitleText.Text = CStr(e.NewValue)
            Catch
            End Try
        End If
    End Sub

    Private Shared Sub OnUnitsChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, GaugeControl)
        If g IsNot Nothing AndAlso (g.IsLoaded Or DesignerProperties.GetIsInDesignMode(g)) Then
            Try
                g.UnitsText.Text = CStr(e.NewValue)
            Catch
            End Try
        End If
    End Sub

    Private Shared Sub OnColorChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim g = TryCast(d, GaugeControl)
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
        Dim g = TryCast(d, GaugeControl)
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

    Private Sub RenderTicks()
        ' Ticks are now static XAML elements, no dynamic rendering needed
    End Sub

    Private Sub UpdateRgbEffectAmount()
        Try
            ' Find the RGBeffect on the root Grid if present and update its Amount property
            Dim root = TryCast(Me.LayoutRoot, FrameworkElement)
            If root Is Nothing Then Return

            ' Amount should be in range 0..0.1 mapped from blur magnitude (e.g., 0..50 -> 0..0.1)
            Dim target = Math.Min(0.1, Math.Max(0.0, Math.Abs(_blurMagnitude) / 50.0 * 0.1))

            Dim effectField = root.Effect
            If effectField IsNot Nothing Then
                Dim effType = effectField.GetType()
                Dim prop = effType.GetProperty("Amount")
                If prop IsNot Nothing Then
                    prop.SetValue(effectField, target)
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub UpdateNeedle()
        ' Direct mapping: needle angle is based on the actual Value between Minimum and Maximum
        Dim valForNeedle = Value
        If valForNeedle < Minimum Then valForNeedle = Minimum
        If valForNeedle > Maximum Then valForNeedle = Maximum

        Dim angle = StartAngle + (valForNeedle - Minimum) / (Maximum - Minimum) * (EndAngle - StartAngle)
        NeedleRotate.Angle = angle
        ValueText.Text = Value.ToString("0")

        ' Calculate velocity
        Dim currentTime = DateTime.Now
        Dim timeDelta = (currentTime - _lastUpdateTime).TotalSeconds
        If timeDelta > 0 Then
            _velocity = (Value - _lastValue) / timeDelta
        End If
        _lastValue = Value
        _lastUpdateTime = currentTime

        ' Decay velocity when not changing
        If Math.Abs(_velocity) < 1 Then
            _velocity *= VelocityDecayRate
        End If

        ' Calculate target blur magnitude
        Dim maxVelocity = 5000.0
        Dim targetBlur = Math.Abs(_velocity) / maxVelocity * 50
        targetBlur = Math.Min(targetBlur, 50)

        ' Smooth transition to target blur (exponential decay)
        _blurMagnitude += (targetBlur - _blurMagnitude) * 0.15

        ' Update blur effects based on needle rotation and velocity
        Dim canvas = NeedleCanvas
        If canvas IsNot Nothing AndAlso canvas.Effect IsNot Nothing Then
            Try
                Dim effectType = canvas.Effect.GetType()
                Dim blurAngleProperty = effectType.GetProperty("BlurAngle")
                Dim blurMagnitudeProperty = effectType.GetProperty("BlurMagnitude")

                If blurAngleProperty IsNot Nothing Then
                    blurAngleProperty.SetValue(canvas.Effect, angle + BlurAngleOffset)
                End If

                If blurMagnitudeProperty IsNot Nothing Then
                    blurMagnitudeProperty.SetValue(canvas.Effect, -_blurMagnitude)
                End If
            Catch
            End Try
        End If
        ' Update RGB chromatic aberration effect amount based on blur
        Try
            UpdateRgbEffectAmount()
        Catch
        End Try
    End Sub

End Class
