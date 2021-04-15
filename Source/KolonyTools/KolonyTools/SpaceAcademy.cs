using System;
using System.Linq;

namespace KolonyTools
{
    public class SpaceAcademy : PartModule
    {
        [KSPField]
        public int MaxLevelKSC = 1; //Kerbin

        [KSPField]
        public int MaxLevelOrbit = 2; //Any orbit

        [KSPField]
        public int MaxLevelLanded = 3; //Any other body

        [KSPEvent(guiActive = true, guiName = "Conduct Training", active = true)]
        public void Training()
        {
            foreach (ProtoCrewMember crew in vessel.GetVesselCrew())
            {
                var oldLevel = crew.experienceLevel;
                var teacher = GetTeacher(crew.experienceTrait.Title);

                //Teachers have to be a level higher than students
                if (teacher.experienceLevel > crew.experienceLevel)
                {
                    // Determine max possible level gain from this teacher
                    var maxGainForSituation = GetSituationLevel();
                    var maxGain = Math.Min(teacher.experienceLevel, crew.experienceLevel + maxGainForSituation);

                    //Then, we simulate training by creating some new flights.  
                    foreach (var flight in teacher.careerLog.GetFlights())
                    {
                        if (crew.experienceLevel < maxGain)
                        {
                            foreach (var logEntry in flight.Entries)
                            {
                                if (!crew.flightLog.HasEntry(logEntry.type, logEntry.target))
                                {
                                    if (logEntry.type != FlightLog.EntryType.PlantFlag.ToString())
                                    {
                                        crew.flightLog.AddEntry(logEntry.type, logEntry.target);
                                        crew.careerLog.AddEntry(logEntry.type, logEntry.target);
                                    }
                                }
                                crew.experience = KerbalRoster.CalculateExperience(crew.flightLog);
                                crew.experienceLevel = KerbalRoster.CalculateExperienceLevel(crew.experience);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (oldLevel < crew.experienceLevel)
                    {
                        string msg = string.Format("{0} trained to level {1} {2}", crew.name, crew.experienceLevel,
                            crew.experienceTrait.Title);
                        ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }
        }

        private ProtoCrewMember GetTeacher(string title)
        {
            var teacherList = vessel.GetVesselCrew().Where(c => c.experienceTrait.Title == title);
            var protoCrewMembers = teacherList as ProtoCrewMember[] ?? teacherList.ToArray();
            if (!protoCrewMembers.Any())
                return null;

            ProtoCrewMember teacher = protoCrewMembers.First();
            foreach (var crew in protoCrewMembers)
            {
                if (crew.experience > teacher.experience)
                    teacher = crew;
            }
            return teacher;
        }

        private int GetSituationLevel()
        {
            var situationLevel = MaxLevelOrbit;
            if (vessel.LandedOrSplashed)
            {
                if (vessel.mainBody.GetName() == "Kerbin")
                {
                    situationLevel = MaxLevelKSC;
                }
                else
                {
                    situationLevel = MaxLevelLanded;
                }
            }
            return situationLevel;
        }
    }
}