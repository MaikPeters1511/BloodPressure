using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;

namespace CleanArchitecture.Presentation.API.Controllers;


[Route("api/[controller]")]
[ApiController]
public class BloodPressureController : ControllerBase
{
    private readonly IBloodPressureRepository _bloodPressureRepository;

    public BloodPressureController(IBloodPressureRepository bloodPressureRepository)
    {
        _bloodPressureRepository = bloodPressureRepository;
    }

    // GET: api/BloodPressure
    [HttpGet]
    public async Task<IEnumerable<BloodPressure>> GetBloodPressures()
    {
        return await _bloodPressureRepository.GetAllAsync();
    }

    // GET: api/BloodPressure/5
    [HttpGet("{id}")]
    public async Task<ActionResult<BloodPressure>> GetBloodPressure(int id)
    {
        var bloodPressure = await _bloodPressureRepository.GetByIdAsync(id);

        if (bloodPressure == null)
        {
            return NotFound();
        }

        return bloodPressure;
    }

    // POST: api/BloodPressure
    [HttpPost]
    public async Task<ActionResult<BloodPressure>> CreateBloodPressure(BloodPressure bloodPressure)
    {
        await _bloodPressureRepository.AddAsync(bloodPressure);

        return CreatedAtAction(nameof(GetBloodPressure), new { id = bloodPressure.Id }, bloodPressure);
    }

    // PUT: api/BloodPressure/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBloodPressure(int id, BloodPressure bloodPressure)
    {
        if (id != bloodPressure.Id)
        {
            return BadRequest();
        }

        var bloodPressureToUpdate = await _bloodPressureRepository.GetByIdAsync(id);

        if (bloodPressureToUpdate == null)
        {
            return NotFound();
        }

        await _bloodPressureRepository.UpdateAsync(bloodPressure);

        return NoContent();
    }

    // DELETE: api/BloodPressure/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBloodPressure(int id)
    {
        var bloodPressureToDelete = await _bloodPressureRepository.GetByIdAsync(id);

        if (bloodPressureToDelete == null)
        {
            return NotFound();
        }

        await _bloodPressureRepository.DeleteAsync(bloodPressureToDelete);

        return NoContent();
    }
}

