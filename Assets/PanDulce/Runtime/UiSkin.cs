using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The hand-drawn chrome art (Assets/PanDulce/Art/UI), bound once at edit time.
    ///
    /// Views generate their children in Build(), which also runs in a player where the
    /// AssetDatabase does not exist — so the sprites have to arrive through a serialized
    /// reference, exactly like PastryDatabase carries the dessert art. StageBuilder fills
    /// this asset in from the folder and hands it to every view that draws chrome.
    ///
    /// Every slot may be left empty: ViewFactory.Plate falls back to the generated rounded
    /// rect the art replaced, so the shell still draws in a checkout without the art.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "Pan Dulce/UI Skin")]
    public sealed class UiSkin : ScriptableObject
    {
        [Tooltip("Top-bar customers chip — the panda face plus its cream number pill.")]
        [SerializeField] Sprite customersChip;

        [Tooltip("Top-bar coin chip — the sakura coin plus its cream number pill.")]
        [SerializeField] Sprite coinChip;

        [Tooltip("The salmon button face: both boost buttons and Play again. 9-sliced.")]
        [SerializeField] Sprite button;

        [Tooltip("The cream plate: boost badges and the run-end card. 9-sliced.")]
        [SerializeField] Sprite plate;

        [Tooltip("The hanging sign — rail, ropes and board in one drawing. 9-sliced.")]
        [SerializeField] Sprite sign;

        [Tooltip("The NEXT plaque — carved wooden frame around a cream face. 9-sliced.")]
        [SerializeField] Sprite nextPlaque;

        [Tooltip("The sakura coin, rim included — the clearance button's price icon.")]
        [SerializeField] Sprite coin;

        [Tooltip("The top bar slab — wood plus its cream flourish. Carries its own bottom edge.")]
        [SerializeField] Sprite topBar;

        [Tooltip("The boost bar slab. Carries its own top edge, sheen included.")]
        [SerializeField] Sprite bottomBar;

        public Sprite CustomersChip => customersChip;
        public Sprite CoinChip => coinChip;
        public Sprite Button => button;
        public Sprite Plate => plate;
        public Sprite Sign => sign;
        public Sprite NextPlaque => nextPlaque;
        public Sprite Coin => coin;
        public Sprite TopBar => topBar;
        public Sprite BottomBar => bottomBar;

#if UNITY_EDITOR
        public void EditorAssign(Sprite customers, Sprite chip, Sprite btn, Sprite pl,
                                 Sprite sgn, Sprite plaque, Sprite coinIcon,
                                 Sprite top, Sprite bottom)
        {
            customersChip = customers;
            coinChip = chip;
            button = btn;
            plate = pl;
            sign = sgn;
            nextPlaque = plaque;
            coin = coinIcon;
            topBar = top;
            bottomBar = bottom;
        }
#endif
    }
}
