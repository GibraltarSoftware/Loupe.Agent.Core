using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupe.Extensibility.Data;
using Microsoft.Extensions.Logging;
using MvcTestApp.Controllers;

namespace MvcTestApp5.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorsController : ControllerBase
    {
        private readonly ILogger<AuthorsController> _logger;
        private readonly List<dynamic> data;

        public AuthorsController(ILogger<AuthorsController> logger)
        {
            _logger = logger;

            data = new List<dynamic>
            {
                new {Id = 1, FirstName = "John", LastName = "Scalzi"},
                new {Id = 2, FirstName = "Isaac", LastName = "Azimov"}
            };
        }

        [HttpGet]
        [ProducesResponseType(200)]
        public IActionResult List()
        {
            return Ok(data);
        }

        [HttpGet]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [Route("{key}")]
        public IActionResult Get([FromRoute] int key)
        {
            var author = data.FirstOrDefault(d => d.Id.Equals(key));

            if (author == null)
            {
                //throw new ArgumentOutOfRangeException("unable to find author by key");
                Gibraltar.Agent.Log.SendSessions(SessionCriteria.ActiveSession);
                return NotFound(key);
            }

            return Ok(author);
        }
    }
}
