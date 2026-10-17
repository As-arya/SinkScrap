using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace SinkScrapt
{
    // Live readout on the middle monitor of the boat computer, readable from the cabin (text faces the -Z side). Dark type on
    // the glowing phosphor, just in front of the glass.
    // Everything comes from the shared server clock, like the HUD and the water itself.
    public class BoatScreen : MonoBehaviour
    {
        TextMeshPro text;
        float next;
        void Start()
        {
            text=gameObject.AddComponent<TextMeshPro>();text.font=Theme.Font;text.fontSize=26;text.color=new Color(.02f,.16f,.1f);
            text.alignment=TextAlignmentOptions.TopLeft;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta=new Vector2(34,26);transform.localScale=Vector3.one*.01f;
        }
        void Update()
        {
            var session=NetSession.Instance;if(text==null||Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
            if(session==null||!session.InExpedition) {var orders=session?.Campaign.orders;text.text="S-04 SIAGA\n\nTOKO SIAP\n"+(orders!=null&&orders.Count>0?"PAKET "+Mathf.CeilToInt(orders.Min(o=>o.remaining))+" DTK":"PESAN ALAT");return;}
            float depth=session.Depth,flood=session.FloodLevel;int deck=1;for(int d=1;d<ShipGrid.Decks;d++)if(flood>=ShipTimeline.FloorHeight(d))deck=d+1;
            text.text="S-04  TENGGELAM\n"+depth.ToString("0.0")+" m\nSISA "+TimeSpan.FromSeconds(session.Remaining).ToString(@"mm\:ss")+"\nAIR DEK "+deck;
        }
    }
}
