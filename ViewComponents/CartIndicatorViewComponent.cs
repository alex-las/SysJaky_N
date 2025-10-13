using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SysJaky_N.Models.ViewModels;
using SysJaky_N.Services;

namespace SysJaky_N.ViewComponents;

public class CartIndicatorViewComponent : ViewComponent
{
    private readonly CartService _cartService;

    public CartIndicatorViewComponent(CartService cartService)
    {
        _cartService = cartService;
    }

    public IViewComponentResult Invoke()
    {
        var items = _cartService.GetItems(HttpContext.Session);
        var itemCount = items.Sum(item => item.Quantity);

        var model = new CartIndicatorViewModel
        {
            ItemCount = itemCount
        };

        return View("/Pages/Shared/_CartIndicator.cshtml", model);
    }
}
