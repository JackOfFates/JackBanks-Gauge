Partial Public Class MainWindow
    Inherits Window

    Public Sub New()
        InitializeComponent()
        ' Ensure ValueSlider maximum matches initial MaxRpmSlider value
        If ValueSlider IsNot Nothing AndAlso MaxRpmSlider IsNot Nothing Then
            ValueSlider.Maximum = MaxRpmSlider.Value
        End If
    End Sub

    Private Sub ValueSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge IsNot Nothing Then
            gauge.Value = e.NewValue
        End If
        If halfGauge IsNot Nothing Then
            halfGauge.Value = e.NewValue
        End If
    End Sub


    Private Sub RedlineSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge IsNot Nothing Then gauge.Redline = RedlineSlider.Value
        If halfGauge IsNot Nothing Then halfGauge.Redline = RedlineSlider.Value
    End Sub

    Private Sub ColorHueSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        Dim hue = ColorHueSlider.Value
        Dim brush = New SolidColorBrush(ColorFromHsv(hue, 0.9, 0.95))
        gauge.Color = brush
        If halfGauge IsNot Nothing Then halfGauge.Color = brush
    End Sub

    Private Sub RedlineHueSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        Dim hue = RedlineHueSlider.Value
        Dim brush = New SolidColorBrush(ColorFromHsv(hue, 0.9, 0.95))
        gauge.RedLineColor = brush
        If halfGauge IsNot Nothing Then halfGauge.RedLineColor = brush
    End Sub

    Private Sub MaxRpmSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        gauge.Maximum = MaxRpmSlider.Value
        If halfGauge IsNot Nothing Then halfGauge.Maximum = MaxRpmSlider.Value
        If ValueSlider IsNot Nothing Then
            ValueSlider.Maximum = MaxRpmSlider.Value
            If ValueSlider.Value > MaxRpmSlider.Value Then
                ValueSlider.Value = MaxRpmSlider.Value
            End If
        End If
    End Sub

    Private Sub MinRpmSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        gauge.Minimum = MinRpmSlider.Value
        If halfGauge IsNot Nothing Then halfGauge.Minimum = MinRpmSlider.Value
        If ValueSlider IsNot Nothing Then
            ValueSlider.Minimum = MinRpmSlider.Value
            If ValueSlider.Value < MinRpmSlider.Value Then
                ValueSlider.Value = MinRpmSlider.Value
            End If
        End If
    End Sub

    Private Sub TrailEnabledCheckBox_Changed(sender As Object, e As RoutedEventArgs)
        Dim enabled = TrailEnabledCheckBox.IsChecked.GetValueOrDefault(True)
        If gauge IsNot Nothing Then gauge.TrailEnabled = enabled
        If halfGauge IsNot Nothing Then halfGauge.TrailEnabled = enabled
    End Sub

    Private Sub TrailSegmentsSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        Dim segments = CInt(TrailSegmentsSlider.Value)
        If gauge IsNot Nothing Then gauge.TrailSegments = segments
        If halfGauge IsNot Nothing Then halfGauge.TrailSegments = segments
    End Sub

    Private Sub TrailDynamicsSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge IsNot Nothing Then gauge.TrailDynamics = TrailDynamicsSlider.Value
        If halfGauge IsNot Nothing Then halfGauge.TrailDynamics = TrailDynamicsSlider.Value
    End Sub

    Private Function ColorFromHsv(h As Double, s As Double, v As Double) As Color
        ' h in [0,360], s,v in [0,1]
        Dim hh = h / 60.0
        Dim i = Math.Floor(hh)
        Dim ff = hh - i
        Dim p = v * (1 - s)
        Dim q = v * (1 - s * ff)
        Dim t = v * (1 - s * (1 - ff))

        Dim r As Double = 0, g As Double = 0, b As Double = 0
        Select Case CInt(i) Mod 6
            Case 0
                r = v : g = t : b = p
            Case 1
                r = q : g = v : b = p
            Case 2
                r = p : g = v : b = t
            Case 3
                r = p : g = q : b = v
            Case 4
                r = t : g = p : b = v
            Case 5
                r = v : g = p : b = q
        End Select

        Return Color.FromArgb(255, CByte(Math.Round(r * 255)), CByte(Math.Round(g * 255)), CByte(Math.Round(b * 255)))
    End Function
End Class
