using UnityEngine;
using System.Collections.Generic;

namespace PathfinderTactics.Characters
{

    [CreateAssetMenu(fileName = "FeatLoadoutSO", menuName = "Scriptable Objects/FeatLoadoutSO")]
    public class FeatLoadoutSO : ScriptableObject
    {
        public List<string> featNames = new List<string>();

        public int featCount => featNames.Count;

        //JSON formatted like this:
        [System.Serializable]
        private class FeatJson
        {
            public FeatSystem system;
            public string name;
        }
        [System.Serializable]
        private class FeatSystem
        {
            public int actions;
            public string description;
            //Other fields can be added as needed (e.g., prerequisites, traits, etc.)
        }

        private FeatJson LoadFeat(string featName)
        {
            string resourcePath = $"feats/{featName}";
            TextAsset ta = Resources.Load<TextAsset>(resourcePath);
            try
            {
                return JsonUtility.FromJson<FeatJson>(ta.text);
            }
            catch
            {
                return null;
            }
        }

        public int GetFeatActions(string featName)
        {
            var data = LoadFeat(featName);

            if (data != null && data.system != null)
                return data.system.actions;

            return 0;
        }

        public string GetFeatDescription(string featName)
        {
            var data = LoadFeat(featName);

            if (data != null && data.system != null)
                return data.system.description;

            return string.Empty;
        }

        public bool FeatExists(string featName)
        {
            return featNames.Contains(featName);
        }

        public bool AddFeat(string featName)
        {
            if (string.IsNullOrEmpty(featName))
                return false;

            // Prevent duplicates
            if (featNames.Contains(featName))
                return false;

            featNames.Add(featName);
            return true;
        }

        

    }
}