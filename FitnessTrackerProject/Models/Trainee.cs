using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessTrackerProject.Models
{
    public class Trainee : BaseUser
    {
        // POLYMORPHISM: Overriding the base method
        public override string GetUserRole()
        {
            return "Trainee";
        }
    } 
}
