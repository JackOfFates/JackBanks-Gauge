# JackBanks Gauge

<img src="JackBanks Gauge/Capture.PNG" alt="Gauge Preview" style="max-width:100%; height:auto;" />

A small WPF gauge control and sample application that renders an animated tachometer-style RPM gauge.

Summary
- Displays a circular gauge with ticks, a needle, center value, units and a configurable redline arc.
- Includes runtime controls for updating the current RPM, maximum RPM, redline percentage and colors (needle and redline hues).
- The sample demonstrates using the `GaugeControl` in a WPF Window with a few sliders to exercise its properties.

Features
- Smooth needle movement and motion blur effect.
- Configurable `Minimum`, `Maximum`, `Value`, `Redline`, `Color` (needle/text color) and `RedLineColor`.
- Tick labels updated dynamically when the min/max range changes.

How to run
1. Open the solution in Visual Studio (recommended: Visual Studio 2022/2024+).
2. Build the solution.
3. Run the `MainWindow` project to see the interactive demo.

Controls (in the demo)
- RPM: sets the current gauge value.
- Redline: sets the redline percentage displayed as the red arc.
- Needle Hue (0–360): changes the needle/text color using HSV hue selection.
- Redline Hue (0–360): changes the redline arc color.
- Maximum RPM: changes the gauge maximum and updates tick labels and needle mapping.

Developer notes
- The control is implemented in `GaugeControl.xaml`/`GaugeControl.xaml.vb`.
- The sample window and sliders are in `MainWindow.xaml`/`MainWindow.xaml.vb`.
- If you want the preview image shown above, add a screenshot to `docs/screenshot.png` (the project already references this path in the README).

License
- MIT

Credits
- Created from the JackBanks Gauge sample project.
