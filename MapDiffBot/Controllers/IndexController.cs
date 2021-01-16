using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapDiffBot.Controllers {
    /// <summary>
    /// Provides an easy route that returns code 200 so uptime monitors can be used
    /// </summary>
    /// <returns>Code 200</returns>
    [Route("")]
    public class IndexController : Controller {
        /// <summary>
        /// Just a route handler
        /// </summary>
        public IActionResult Index() {
            return Ok("Welcome to MapDiffBot");
        }
    }
}
