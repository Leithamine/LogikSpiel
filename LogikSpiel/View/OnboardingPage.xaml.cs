using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class OnboardingPage : ContentPage
{
	public OnboardingPage(OnboardingViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}