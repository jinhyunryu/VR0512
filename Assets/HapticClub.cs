using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class HapticClub : MonoBehaviour
{
            public float hapticDuration = 0.1f;
        public float hapticStrength = 0.5f;

        public HapticImpulsePlayer rightHaptic, leftHaptic;

    public void HapticFx()
    {
        if (rightHaptic != null)
        {
            rightHaptic.SendHapticImpulse(hapticStrength, hapticDuration);
        }

        if (leftHaptic != null)
        {
            leftHaptic.SendHapticImpulse(hapticStrength, hapticDuration);
        }
    }

}
