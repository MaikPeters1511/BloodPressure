using CleanArchitecture.Domain.Entities;
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

         // GET: api/BloodPressures
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BloodPressure>>> GetBloodPressure()
        {
            return await _context.BloodPressures.ToListAsync();
        }

        // GET: api/BloodPressures/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BloodPressure>> GetBloodPressure(int id)
        {
            var bloodPressure = await _context.BloodPressures.FindAsync(id);

            if (bloodPressure == null)
            {
                return NotFound();
            }

            return bloodPressure;
        }

        // PUT: api/BloodPressures/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBloodPressure(int id, BloodPressure bloodPressure)
        {
            if (id != bloodPressure.Id)
            {
                return BadRequest();
            }

            _context.Entry(bloodPressure).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BloodPressureExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/BloodPressures
        [HttpPost]
        public async Task<ActionResult<BloodPressure>> PostBloodPressure(BloodPressure bloodPressure)
        {
            _context.BloodPressures.Add(bloodPressure);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBloodPressure", new { id = bloodPressure.Id }, bloodPressure);
        }

        // DELETE: api/BloodPressures/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBloodPressure(int id)
        {
            var bloodPressure = await _context.BloodPressures.FindAsync(id);
            if (bloodPressure == null)
            {
                return NotFound();
            }

            _context.BloodPressures.Remove(bloodPressure);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool BloodPressureExists(int id)
        {
            return _context.BloodPressures.Any(e => e.Id == id);
        }

    }
}
