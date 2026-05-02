using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TacticsGame.Story
{
    public class StoryData
    {
        public List<StoryActorData> actors;
        public List<StoryAction> sequence;
    }

    public class StoryActorData
    {
        public string id;
        public string sceneObject; // GameObject name or StoryActorID
    }

    [JsonConverter(typeof(StoryActionConverter))]
    public abstract class StoryAction
    {
        public string type;
        public string actor;
        public bool waitUntilFinished = true; // Default behavior
    }

    public class DialogueAction : StoryAction
    {
        public string text;
    }

    public class MoveAction : StoryAction
    {
        public Vector3 destination;
        public float speed;
    }

    public class AnimateAction : StoryAction
    {
        public string triggerName;
    }

    public class CameraMoveAction : StoryAction
    {
        public string target; // Target actor ID
        public float duration;
        public Vector3 offset;
    }

    public class CameraShakeAction : StoryAction
    {
        public float intensity;
        public float duration;
    }

    public class CameraSetAction : StoryAction
    {
        public Vector3 position;
        public Vector3 rotation;
        public float duration;
    }

    public class SceneLoadAction : StoryAction
    {
        public string sceneName;
    }

    public class StoryActionConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType)
        {
            return typeof(StoryAction).IsAssignableFrom(objectType);
        }

        public override object ReadJson(
            JsonReader reader,
            System.Type objectType,
            object existingValue,
            JsonSerializer serializer
        )
        {
            JObject jo = JObject.Load(reader);
            string type = (string)jo["type"];

            StoryAction action = type switch
            {
                "Dialogue" => new DialogueAction(),
                "Move" => new MoveAction(),
                "Animate" => new AnimateAction(),
                "CameraMove" => new CameraMoveAction(),
                "CameraShake" => new CameraShakeAction(),
                "CameraSet" => new CameraSetAction(),
                "SceneLoad" => new SceneLoadAction(),
                _ => throw new System.Exception($"Unknown StoryAction type: {type}"),
            };

            action.type = type;
            if (jo["actor"] != null)
                action.actor = (string)jo["actor"];
            if (jo["waitUntilFinished"] != null)
                action.waitUntilFinished = (bool)jo["waitUntilFinished"];

            JToken paramToken = jo["params"];
            if (paramToken != null)
            {
                if (action is DialogueAction da)
                {
                    da.text = (string)paramToken["text"];
                }
                else if (action is MoveAction ma)
                {
                    JToken dest = paramToken["destination"];
                    ma.destination = new Vector3(
                        (float)dest["x"],
                        (float)dest["y"],
                        (float)dest["z"]
                    );
                    ma.speed = (float)paramToken["speed"];
                }
                else if (action is AnimateAction aa)
                {
                    aa.triggerName = (string)paramToken["triggerName"];
                }
                else if (action is CameraMoveAction cma)
                {
                    cma.target = (string)paramToken["target"];
                    cma.duration = (float)paramToken["duration"];
                    if (paramToken["offset"] != null)
                    {
                        JToken off = paramToken["offset"];
                        cma.offset = new Vector3((float)off["x"], (float)off["y"], (float)off["z"]);
                    }
                }
                else if (action is CameraShakeAction csa)
                {
                    csa.intensity = (float)paramToken["intensity"];
                    csa.duration = (float)paramToken["duration"];
                }
                else if (action is CameraSetAction cset)
                {
                    if (paramToken["position"] != null)
                    {
                        JToken pos = paramToken["position"];
                        cset.position = new Vector3(
                            (float)pos["x"],
                            (float)pos["y"],
                            (float)pos["z"]
                        );
                    }
                    if (paramToken["rotation"] != null)
                    {
                        JToken rot = paramToken["rotation"];
                        cset.rotation = new Vector3(
                            (float)rot["x"],
                            (float)rot["y"],
                            (float)rot["z"]
                        );
                    }
                    cset.duration = (float)(paramToken["duration"] ?? 0f);
                }
                else if (action is SceneLoadAction sla)
                {
                    sla.sceneName = (string)paramToken["sceneName"];
                }
            }

            return action;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new System.NotImplementedException();
        }
    }
}
