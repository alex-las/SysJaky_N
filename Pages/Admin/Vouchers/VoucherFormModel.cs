using Microsoft.AspNetCore.Mvc.Rendering;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Vouchers;

public class VoucherFormModel
{
    public VoucherFormModel(Voucher voucher, IEnumerable<SelectListItem> courses)
    {
        Voucher = voucher;
        Courses = courses;
    }

    public Voucher Voucher { get; }

    public IEnumerable<SelectListItem> Courses { get; }
}
