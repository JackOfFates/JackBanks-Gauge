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
    End Sub


    Private Sub RedlineSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        gauge.Redline = RedlineSlider.Value
    End Sub

    Private Sub ColorHueSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        Dim hue = ColorHueSlider.Value
        gauge.Color = New SolidColorBrush(ColorFromHsv(hue, 0.9, 0.95))
    End Sub

    Private Sub RedlineHueSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        Dim hue = RedlineHueSlider.Value
        gauge.RedLineColor = New SolidColorBrush(ColorFromHsv(hue, 0.9, 0.95))
    End Sub

    Private Sub MaxRpmSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If gauge Is Nothing Then Return
        gauge.Maximum = MaxRpmSlider.Value
        If ValueSlider IsNot Nothing Then
            ValueSlider.Maximum = MaxRpmSlider.Value
            ' Clamp current value to new maximum
            If ValueSlider.Value > MaxRpmSlider.Value Then
                ValueSlider.Value = MaxRpmSlider.Value
            End If
        End If
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
