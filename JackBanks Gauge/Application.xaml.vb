Imports System.Windows.Interop

Class Application

    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        ' Ensure hardware rendering is used (V-Sync is enabled through DWM composition)
        RenderOptions.ProcessRenderMode = RenderMode.Default
    End Sub

End Class
