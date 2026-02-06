
namespace EcommerceApp.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        public string? IconKey { get; set; } 

        public string? IconClass { get; set; } 
        public string? IconColor { get; set; }  
        public string? IconBgColor { get; set; }

        public bool IsCore { get; set; } = false;
    }
}
