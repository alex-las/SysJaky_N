namespace SysJaky_N.Models;

public class NewsletterSubscriberCategory
{
    public int NewsletterSubscriberId { get; set; }

    public NewsletterSubscriber NewsletterSubscriber { get; set; } = default!;

    public int CourseCategoryId { get; set; }

    public CourseCategory CourseCategory { get; set; } = default!;
}
