using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using SysJaky_N.Authorization;
using SysJaky_N.Data;
using SysJaky_N.Pages.Orders;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.Orders;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DetailsModel : OrderDetailsPageModel
{
    private readonly IStringLocalizer<DetailsModel> _localizer;

    public DetailsModel(
        ApplicationDbContext context,
        IConfiguration configuration,
        PaymentService paymentService,
        IConverter converter,
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IStringLocalizer<DetailsModel> localizer)
        : base(context, configuration, paymentService, converter, viewEngine, tempDataProvider)
    {
        _localizer = localizer;
    }

    protected override bool RequireOrderOwnership => false;
    protected override string PaymentReturnPagePath => _localizer["PaymentReturnPagePath"];
}
