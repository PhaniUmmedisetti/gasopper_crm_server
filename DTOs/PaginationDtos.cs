using System.ComponentModel.DataAnnotations;

namespace gasopper_crm_server.DTOs
{
    // SHARED: Common pagination DTOs used by both Leads and Opportunities
    public class PaginationDto
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
    }

    // SHARED: Base response for paginated data
    public class PaginatedResponseDto<T>
    {
        public List<T> Data { get; set; } = new List<T>();
        public PaginationDto Pagination { get; set; } = new PaginationDto();
    }

    // SHARED: Common filter base class
    public abstract class BaseFilters
    {
        public string? Search { get; set; }
        public int? StatusId { get; set; }
        public int? AssignedTo { get; set; }
        public bool IncludeDeleted { get; set; } = false;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}