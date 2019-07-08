using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace SavageCodes.Networking.ClientSidePrediction
{
    public class Utility
    {
        public static Vector3 InterpTo(Vector3 start, Vector3 end,float speed)
        {

            // Distance to reach
            Vector3 dist = end - start;

            // If distance is too small, just set the desired location
            if( dist.sqrMagnitude < 0.00001f)
            {
                return end;
            }

            // Delta Move, Clamp so we do not over shoot.
            Vector3	DeltaMove = dist * Mathf.Clamp(Time.deltaTime * speed, 0, 1);

            return start + DeltaMove;
        }
        
        public static float InterpTo(float start, float end,float speed)
        {

            // Distance to reach
            float dist = start - end;

            // If distance is too small, just set the desired location
            if( Mathf.Abs(dist) < 0.00001f)
            {
                return end;
            }

            // Delta Move, Clamp so we do not over shoot.
            float DeltaMove = dist * Mathf.Clamp(Time.deltaTime * speed, 0, 1);

            return start + DeltaMove;
        }
    }
}
