#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Один раз, до первой рекламы: можно ли показывать рекламу с учётом интересов. Без ответа нельзя —
    /// Яндекс требует передавать согласие при каждом запуске, а закон о персональных данных — спрашивать.
    /// Ответ меняется в настройках («Персональная реклама»).
    /// </summary>
    public sealed class ConsentPopup : Popup
    {
        public ConsentPopup(App app)
            : base(app, true)
        {
            AddIcon(KnittedPicture.Game.Icon.Gift, Palette.Primary, Palette.PrimarySoft, 64);
            AddTitle(L.T("Реклама в игре"), 24);
            AddText(L.T("Игра бесплатная: её поддерживает реклама. Разрешить показывать рекламу с учётом твоих интересов? ") +
                    L.T("Если нет, реклама будет обычной. Ответ можно поменять в настройках."), 15, 4);
            AddPrimary(L.T("Разрешить")).OnClick(() =>
            {
                app.SetAdsConsent(true);
                Close();
            });
            AddLink(L.T("Нет, обычная реклама")).OnClick(() =>
            {
                app.SetAdsConsent(false);
                Close();
            });
            AddLink(L.T("Политика конфиденциальности")).OnClick(() => Application.OpenURL(StoreConfig.PrivacyPolicyUrl));
            Finish(8);
        }

        public override bool Closable => false;

        protected override bool CloseOnDim => false;
    }
}
