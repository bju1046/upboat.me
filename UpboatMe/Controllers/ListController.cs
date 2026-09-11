using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using UpboatMe.Models;

namespace UpboatMe.Controllers
{
    [ApiController]
    [Route("api/list")]
    public class ListController : ControllerBase
    {
        [HttpGet]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult Get()
        {
            var result = GlobalMemeConfiguration
                .Memes.GetMemes()
                .Select(m => new ApiMemeResult
                {
                    Name = m.Aliases.Last(),
                    Description = m.Description,
                    Aliases = m.Aliases,
                });

            return Ok(result);
        }

        [HttpGet("{id}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult Get(string id)
        {
            var meme = GlobalMemeConfiguration.Memes[id];
            if (meme == null)
            {
                return NotFound();
            }

            var result = new ApiMemeResult
            {
                Name = meme.Aliases.Last(),
                Description = meme.Description,
                Aliases = meme.Aliases,
            };

            return Ok(result);
        }
    }
}
