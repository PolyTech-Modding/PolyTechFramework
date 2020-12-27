using System.IO;
using PolyTechFramework;

namespace PolyTechFramework
{
    class PolyTechUtils
    {
        public static void setModdedSimSpeeds()
        {
            Bridge.NUM_SIMULATION_SPEEDS = 11;
            Bridge.DEFAULT_SIMULATION_SPEED_INDEX = 5;
            Bridge.m_SimulationSpeeds = new float[]
            {
                0.0000001f,
                0.05f,
                0.1f,
                0.2f,
                0.5f,
                1f,
                2f,
                3f,
                6f,
                12f,
                24f
            };
        }
        public static void setVanillaSimSpeeds()
        {
            Bridge.NUM_SIMULATION_SPEEDS = 5;
            Bridge.DEFAULT_SIMULATION_SPEED_INDEX = 2;
            Bridge.m_SimulationSpeeds = new float[]
            {
                0.2f,
                0.5f,
                1f,
                2f,
                3f
            };
        }
        public static void setReplaysModded()
        {
            if (GamePersistentPath.GetPersistentDataDirectory() == null) return;
            Replays.m_Path = Path.Combine(GamePersistentPath.GetPersistentDataDirectory(), Replays.REPLAYS_DIRECTORY);
            Replays.m_Path = Path.Combine(Replays.m_Path, "modded");
            Utils.TryToCreateDirectory(Replays.m_Path);
        }
        public static void setReplaysVanilla()
        {
            Replays.m_Path = Path.Combine(GamePersistentPath.GetPersistentDataDirectory(), Replays.REPLAYS_DIRECTORY);
        }
        public static void setVersion()
        {
        }
    }
}
