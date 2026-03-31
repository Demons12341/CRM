namespace ProjectManagementSystem.Models.DTOs
{
    public class MenuOptionDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<MenuOptionDto> Children { get; set; } = new();
    }

    public class UpdateRoleMenuPermissionsRequest
    {
        public List<string> MenuCodes { get; set; } = new();
    }
}
