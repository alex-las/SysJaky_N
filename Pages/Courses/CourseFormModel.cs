using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

public class CourseFormModel
{
    public Course Course { get; set; } = new();

    public SelectList CourseGroups { get; set; } = default!;

    public IFormFile? CoverImage { get; set; }

    public IHtmlContent? ActionButtons { get; set; }
}
