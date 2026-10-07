// Тактильный отклик на iPhone: лёгкий тап, мягкий, средний, «успех» и «внимание».
// Вызывается из Haptics.cs через DllImport("__Internal").
#import <UIKit/UIKit.h>

extern "C" void KnittedHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (kind == 3 || kind == 4)
        {
            UINotificationFeedbackGenerator *notify = [[UINotificationFeedbackGenerator alloc] init];
            [notify notificationOccurred:(kind == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeWarning)];
            return;
        }

        UIImpactFeedbackStyle style = kind == 0 ? UIImpactFeedbackStyleLight
            : kind == 1 ? UIImpactFeedbackStyleSoft
            : UIImpactFeedbackStyleMedium;
        UIImpactFeedbackGenerator *impact = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        [impact impactOccurred];
    });
}
