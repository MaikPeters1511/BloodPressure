using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Presentation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BloodPressureController : ControllerBase
    {
        private readonly AddPostgresSqlDbContext _context;

        public BloodPressureController(AddPostgresSqlDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<IActionResult> GetBloodPressure()
        {
            var result = await _context.BloodPressures.ToListAsync();
            return Ok(result);
        }

    }
}
