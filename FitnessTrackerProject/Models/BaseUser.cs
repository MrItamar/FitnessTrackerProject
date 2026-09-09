using System;

namespace FitnessTrackerProject.Models
{
    // 'abstract' means we can never create just a "BaseUser". 
    // They MUST be a Coach or a Trainee.
    public abstract class BaseUser
    {
        // 1. ENCAPSULATION: Private fields hidden from the rest of the app
        private string _username;
        private int _age;

        // 2. ENCAPSULATION: Public properties to safely access the data
        public string Username
        {
            get { return _username; }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name cannot be empty.");
                _username = value;
            }
        }

        public int Age
        {
            get { return _age; }
            set
            {
                if (value < 10 || value > 120) throw new ArgumentException("Please enter a valid age.");
                _age = value;
            }
        }

        // Auto-properties for simpler data
        public string Gender { get; set; }
        public string ProfilePicPath { get; set; }

        // 3. POLYMORPHISM: An abstract method that child classes MUST override
        public abstract string GetUserRole();
    }
}