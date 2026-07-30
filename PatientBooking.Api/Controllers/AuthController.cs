using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PatientBooking.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public class AuthController : BaseApiController
{
    // GET: api/<AuthController>
    [HttpGet]
    public IEnumerable<string> Get()
    {
        return new string[] { "value1", "value2" };
    }

    // GET api/<AuthController>/5
    [HttpGet("{id}")]
    public string Get(int id)
    {
        return "value";
    }

    // POST api/<AuthController>
    [HttpPost]
    public void Post([FromBody] string value)
    {
        // Method intentionally left empty.
    }

    // PUT api/<AuthController>/5
    [HttpPut("{id}")]
    public void Put(int id, [FromBody] string value)
    {
        // Method intentionally left empty.
    }

    // DELETE api/<AuthController>/5
    [HttpDelete("{id}")]
    public void Delete(int id)
    {
        // Method intentionally left empty.
    }
}
