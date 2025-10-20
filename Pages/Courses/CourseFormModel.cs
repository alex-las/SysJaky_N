using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SysJaky_N.Models;
using System.Collections.Generic;
using System.Linq;

namespace SysJaky_N.Pages.Courses;

public class CourseFormModel
{
    public Course Course { get; set; } = new();

    public SelectList CourseGroups { get; set; } = default!;

    public IFormFile? CoverImage { get; set; }

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IList<int> SelectedCategoryIds { get; set; } = new List<int>();

    public IHtmlContent? ActionButtons { get; set; }
}
