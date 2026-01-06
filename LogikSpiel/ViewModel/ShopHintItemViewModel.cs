// LogikSpiel/ViewModel/ShopHintItemViewModel.cs
#nullable enable
using LogikSpiel.Core;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.ViewModel;

public sealed class ShopHintItemViewModel : ObservableObject
{
    private bool _isPurchased;

    public ShopHintItemViewModel(string id, string title, string description, int price)
    {
        Id = id;
        Title = title;
        Description = description;
        Price = price;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public int Price { get; }

    public bool IsPurchased
    {
        get => _isPurchased;
        set
        {
            if (SetProperty(ref _isPurchased, value))
            {
                OnPropertyChanged(nameof(CanPurchase));
                OnPropertyChanged(nameof(PurchaseLabel));
            }
        }
    }

    public bool CanPurchase => !IsPurchased;

    public string PurchaseLabel => IsPurchased
        ? LocalizationService.GetString("Common_Purchased")
        : LocalizationService.Format("Common_PriceCoinsFormat", Price);
}
