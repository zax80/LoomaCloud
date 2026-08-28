namespace LoomaCloud.UI.MAUI.Desktop;

// "Application" alone is ambiguous here: this project references
// LoomaCloud.Application, which puts a namespace called "Application" inside
// "LoomaCloud". Enclosing-namespace members are resolved before any
// file-level using (even a using-alias), so an alias for "Application"
// declared at the top of this file loses to that namespace match — hence
// the full qualification below instead. Same collision will hit any
// other file in this project that uses "Application" unqualified.
public partial class App : Microsoft.Maui.Controls.Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}