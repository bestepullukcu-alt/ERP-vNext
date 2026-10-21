using Diten.Platform.Domain.Entities.Notifications;
using PreOrderEvents = Diten.Platform.Application.Features.Crm.SelfRegistration.CrmManifestProvider;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/*
 * WP-VW-W6 W6-BE-c1 — the seven MOD-0168 pre-order e-mails CRM sends through the internal event dispatch, in all seven
 * tenant languages, in the shell form (heading, information table, one action, footnote) of the 06 mockup's sip_* mails
 * plus the DESIGN-GAP-W6 §3 additions (changes requested, cancelled, automatic approval). The variables are display text
 * CRM formats (dates yyyy-MM-dd, the total "1234.50 TRY"); a row whose value is empty is left out by the shell, and an
 * empty subject part is left out with its separator (W5-FIX2 F-4). The subject is the event + the order number + the
 * pharmacy — never a person's name. The depot and the pharmacy are EXTERNAL: their mails carry no CRM link (CRM leaves
 * orderUrl empty for them, which hides the button) and say that this is a pre-order the depot invoices. The depot copy and
 * the pharmacy copy get their PDF attachment in W6-BE-c2.
 */
public static partial class NotificationTemplateSeed
{
    /// <summary>The words every pre-order e-mail shares, per language.</summary>
    private sealed record PreOrderWords(
        string Pharmacy, string Rep, string Depot, string Delivery, string Lines, string Total, string Reason, string Note,
        string OrderNo, string Expires, string OpenOrder, string FootInternal, string FootExternal);

    private static PreOrderWords PreOrderWordsFor(string locale) => locale switch
    {
        "en" => new("Pharmacy", "Representative", "Depot", "Delivery", "Lines", "Total", "Reason", "Note", "Order no.", "Link valid until",
            "Open the order", "Sent automatically by CRM.",
            "This is a pre-order; the depot issues the invoice. Sent automatically by CRM on behalf of {{sellerName}}."),
        "tr" => new("Eczane", "Temsilci", "Depo", "Teslim", "Kalem", "Toplam", "Gerekçe", "Not", "Sipariş no", "Bağlantı geçerlilik",
            "Siparişi aç", "CRM tarafından otomatik gönderildi.",
            "Bu bir ön sipariştir; faturayı depo düzenler. {{sellerName}} adına CRM tarafından otomatik gönderildi."),
        "fr" => new("Pharmacie", "Délégué", "Dépositaire", "Livraison", "Lignes", "Total", "Motif", "Note", "N° de commande", "Lien valable jusqu'au",
            "Ouvrir la commande", "Envoyé automatiquement par le CRM.",
            "Ceci est une pré-commande ; le dépositaire établit la facture. Envoyé automatiquement par le CRM pour le compte de {{sellerName}}."),
        "es" => new("Farmacia", "Representante", "Distribuidor", "Entrega", "Líneas", "Total", "Motivo", "Nota", "N.º de pedido", "Enlace válido hasta",
            "Abrir el pedido", "Enviado automáticamente por el CRM.",
            "Esto es un prepedido; el distribuidor emite la factura. Enviado automáticamente por el CRM en nombre de {{sellerName}}."),
        "zh" => new("药店", "代表", "经销商", "交货", "品项", "合计", "原因", "备注", "订单号", "链接有效期至",
            "打开订单", "由 CRM 自动发送。",
            "这是一份预订单；发票由经销商开具。由 CRM 代表 {{sellerName}} 自动发送。"),
        "ar" => new("الصيدلية", "المندوب", "المستودع", "التسليم", "البنود", "الإجمالي", "السبب", "ملاحظة", "رقم الطلب", "الرابط صالح حتى",
            "فتح الطلب", "أُرسلت تلقائيًا من نظام CRM.",
            "هذا طلب مسبق؛ يُصدر المستودع الفاتورة. أُرسلت تلقائيًا من نظام CRM نيابةً عن {{sellerName}}."),
        "ru" => new("Аптека", "Представитель", "Дистрибьютор", "Доставка", "Позиций", "Итого", "Причина", "Примечание", "№ заказа", "Ссылка действует до",
            "Открыть заказ", "Отправлено автоматически системой CRM.",
            "Это предварительный заказ; счёт выставляет дистрибьютор. Отправлено автоматически системой CRM от имени {{sellerName}}."),
        _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
    };

    /// <summary>One event's own words in one language.</summary>
    private sealed record PreOrderMail(string Subject, string Heading, string Intro, string? Notice = null, string? Action = null);

    private static IEnumerable<NotificationTemplate> PreOrderTemplates() =>
        TenantLocales.SelectMany(locale => new[]
        {
            PreOrderApprovalRequested(locale),
            PreOrderAutoApproved(locale),
            PreOrderChangesRequested(locale),
            PreOrderRejected(locale),
            PreOrderTransmittedDepot(locale),
            PreOrderPharmacyCopy(locale),
            PreOrderCancelled(locale)
        });

    /// <summary><c>commercial.preorder.approvalrequested</c> — to the approver(s): the one-time, signed-in link (D3, D4).</summary>
    private static NotificationTemplate PreOrderApprovalRequested(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Approval needed · Pre-order {{orderCode}} · {{pharmacyName}}", "A pre-order is waiting for your approval",
                "{{repName}} created a pre-order for {{pharmacyName}}; the pharmacist has signed it. After your approval it is sent to the depot.",
                "The link works once and only after you sign in.", "Open the approval"),
            "tr" => new PreOrderMail("Onayınız bekleniyor · Ön sipariş {{orderCode}} · {{pharmacyName}}", "Ön sipariş onayınızı bekliyor",
                "{{repName}}, {{pharmacyName}} için ön sipariş oluşturdu; eczacı imzası alındı. Onayınızdan sonra depoya iletilir.",
                "Bağlantı bir kez ve yalnız oturum açtıktan sonra çalışır.", "Onay ekranını aç"),
            "fr" => new PreOrderMail("Validation requise · Pré-commande {{orderCode}} · {{pharmacyName}}", "Une pré-commande attend votre validation",
                "{{repName}} a créé une pré-commande pour {{pharmacyName}} ; le pharmacien l'a signée. Après votre validation, elle est transmise au dépositaire.",
                "Le lien ne fonctionne qu'une fois, après connexion.", "Ouvrir la validation"),
            "es" => new PreOrderMail("Aprobación pendiente · Prepedido {{orderCode}} · {{pharmacyName}}", "Un prepedido espera su aprobación",
                "{{repName}} ha creado un prepedido para {{pharmacyName}}; el farmacéutico lo ha firmado. Tras su aprobación se envía al distribuidor.",
                "El enlace funciona una sola vez y solo después de iniciar sesión.", "Abrir la aprobación"),
            "zh" => new PreOrderMail("待您审批 · 预订单 {{orderCode}} · {{pharmacyName}}", "有一份预订单等待您审批",
                "{{repName}} 为 {{pharmacyName}} 创建了预订单，药剂师已签名。经您审批后将发送给经销商。",
                "该链接仅可使用一次，且须登录后使用。", "打开审批页面"),
            "ar" => new PreOrderMail("بانتظار موافقتك · طلب مسبق {{orderCode}} · {{pharmacyName}}", "طلب مسبق بانتظار موافقتك",
                "أنشأ {{repName}} طلبًا مسبقًا لصيدلية {{pharmacyName}}، ووقّع عليه الصيدلي. بعد موافقتك يُرسل إلى المستودع.",
                "يعمل الرابط مرة واحدة فقط وبعد تسجيل الدخول.", "فتح صفحة الموافقة"),
            "ru" => new PreOrderMail("Требуется согласование · Предзаказ {{orderCode}} · {{pharmacyName}}", "Предзаказ ожидает вашего согласования",
                "{{repName}} оформил(а) предзаказ для аптеки {{pharmacyName}}; провизор его подписал. После согласования он будет передан дистрибьютору.",
                "Ссылка работает один раз и только после входа в систему.", "Открыть согласование"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderApprovalRequestedEvent, locale, m,
            [(w.Pharmacy, "{{pharmacyName}}"), (w.Rep, "{{repName}}"), (w.Depot, "{{depotName}}"), (w.Delivery, "{{deliveryDate}}"),
             (w.Lines, "{{lineCount}}"), (w.Total, "{{totalText}}"), (w.Expires, "{{expiresAt}}")],
            actionUrlVariable: "approvalUrl", footnote: w.FootInternal, required: ["approvalUrl"]);
    }

    /// <summary><c>commercial.preorder.autoapproved</c> — to the rep (manager in copy): approved by the tenant setting (D1).</summary>
    private static NotificationTemplate PreOrderAutoApproved(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Approved automatically · Pre-order {{orderCode}} · {{pharmacyName}}", "The pre-order was approved automatically",
                "The pre-order for {{pharmacyName}} was within every limit and was approved automatically (tenant setting). It is being sent to the depot.",
                "For information; nothing to do."),
            "tr" => new PreOrderMail("Kendiliğinden onaylandı · Ön sipariş {{orderCode}} · {{pharmacyName}}", "Ön sipariş kendiliğinden onaylandı",
                "{{pharmacyName}} ön siparişi tüm sınırların içindeydi ve kendiliğinden onaylandı (kiracı ayarı). Depoya iletiliyor.",
                "Bilgi amaçlıdır; işlem gerekmez."),
            "fr" => new PreOrderMail("Validée automatiquement · Pré-commande {{orderCode}} · {{pharmacyName}}", "La pré-commande a été validée automatiquement",
                "La pré-commande pour {{pharmacyName}} respectait toutes les limites et a été validée automatiquement (paramètre du locataire). Elle est transmise au dépositaire.",
                "Pour information ; aucune action requise."),
            "es" => new PreOrderMail("Aprobado automáticamente · Prepedido {{orderCode}} · {{pharmacyName}}", "El prepedido se aprobó automáticamente",
                "El prepedido de {{pharmacyName}} estaba dentro de todos los límites y se aprobó automáticamente (configuración del inquilino). Se está enviando al distribuidor.",
                "Para su información; no requiere ninguna acción."),
            "zh" => new PreOrderMail("已自动审批 · 预订单 {{orderCode}} · {{pharmacyName}}", "预订单已自动审批通过",
                "{{pharmacyName}} 的预订单在所有限制范围内，已自动审批通过（租户设置），正在发送给经销商。",
                "仅供参考，无需操作。"),
            "ar" => new PreOrderMail("تمت الموافقة تلقائيًا · طلب مسبق {{orderCode}} · {{pharmacyName}}", "تمت الموافقة على الطلب المسبق تلقائيًا",
                "كان الطلب المسبق لصيدلية {{pharmacyName}} ضمن جميع الحدود وتمت الموافقة عليه تلقائيًا (إعداد المستأجر). يجري إرساله إلى المستودع.",
                "للعلم فقط؛ لا يلزم أي إجراء."),
            "ru" => new PreOrderMail("Согласовано автоматически · Предзаказ {{orderCode}} · {{pharmacyName}}", "Предзаказ согласован автоматически",
                "Предзаказ для аптеки {{pharmacyName}} уложился во все лимиты и согласован автоматически (настройка арендатора). Он передаётся дистрибьютору.",
                "Для информации; действий не требуется."),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderAutoApprovedEvent, locale, m with { Action = w.OpenOrder },
            [(w.Pharmacy, "{{pharmacyName}}"), (w.Depot, "{{depotName}}"), (w.Delivery, "{{deliveryDate}}"), (w.Lines, "{{lineCount}}"),
             (w.Total, "{{totalText}}")],
            actionUrlVariable: "orderUrl", footnote: w.FootInternal);
    }

    /// <summary><c>commercial.preorder.changesrequested</c> — to the rep: back to the draft, the pharmacist signs again (D2).</summary>
    private static NotificationTemplate PreOrderChangesRequested(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Changes requested · Pre-order {{orderCode}} · {{pharmacyName}}", "Changes were requested on your pre-order",
                "The approver sent the pre-order for {{pharmacyName}} back to you. It is a draft again; the pharmacist's signature has to be taken again."),
            "tr" => new PreOrderMail("Değişiklik istendi · Ön sipariş {{orderCode}} · {{pharmacyName}}", "Ön siparişinizde değişiklik istendi",
                "Onaycı {{pharmacyName}} ön siparişini size geri gönderdi. Sipariş yeniden taslakta; eczacı imzasının yeniden alınması gerekir."),
            "fr" => new PreOrderMail("Modifications demandées · Pré-commande {{orderCode}} · {{pharmacyName}}", "Des modifications ont été demandées sur votre pré-commande",
                "Le valideur vous a renvoyé la pré-commande pour {{pharmacyName}}. Elle est de nouveau en brouillon ; la signature du pharmacien doit être recueillie à nouveau."),
            "es" => new PreOrderMail("Cambios solicitados · Prepedido {{orderCode}} · {{pharmacyName}}", "Se solicitaron cambios en su prepedido",
                "El aprobador le ha devuelto el prepedido de {{pharmacyName}}. Vuelve a estar en borrador; hay que recoger de nuevo la firma del farmacéutico."),
            "zh" => new PreOrderMail("需要修改 · 预订单 {{orderCode}} · {{pharmacyName}}", "您的预订单需要修改",
                "审批人已将 {{pharmacyName}} 的预订单退回给您。订单已恢复为草稿，需要重新获取药剂师签名。"),
            "ar" => new PreOrderMail("طُلبت تعديلات · طلب مسبق {{orderCode}} · {{pharmacyName}}", "طُلبت تعديلات على طلبك المسبق",
                "أعاد المعتمِد الطلب المسبق لصيدلية {{pharmacyName}} إليك. أصبح مسودة من جديد، ويجب أخذ توقيع الصيدلي مرة أخرى."),
            "ru" => new PreOrderMail("Требуются изменения · Предзаказ {{orderCode}} · {{pharmacyName}}", "По вашему предзаказу запрошены изменения",
                "Согласующий вернул вам предзаказ для аптеки {{pharmacyName}}. Он снова в черновике; подпись провизора нужно получить заново."),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderChangesRequestedEvent, locale, m with { Action = w.OpenOrder },
            [(w.Reason, "{{reason}}"), (w.Pharmacy, "{{pharmacyName}}"), (w.Lines, "{{lineCount}}")],
            actionUrlVariable: "orderUrl", footnote: w.FootInternal, required: ["reason"]);
    }

    /// <summary><c>commercial.preorder.rejected</c> — to the rep: the order is closed and can be copied into a new one (D6).</summary>
    private static NotificationTemplate PreOrderRejected(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Rejected · Pre-order {{orderCode}} · {{pharmacyName}}", "Your pre-order was rejected",
                "The approver rejected the pre-order for {{pharmacyName}}. It will not be sent to the depot; you can copy it into a new order."),
            "tr" => new PreOrderMail("Reddedildi · Ön sipariş {{orderCode}} · {{pharmacyName}}", "Ön siparişiniz reddedildi",
                "Onaycı {{pharmacyName}} ön siparişini reddetti. Depoya iletilmeyecek; siparişi kopyalayıp yeni sipariş açabilirsiniz."),
            "fr" => new PreOrderMail("Refusée · Pré-commande {{orderCode}} · {{pharmacyName}}", "Votre pré-commande a été refusée",
                "Le valideur a refusé la pré-commande pour {{pharmacyName}}. Elle ne sera pas transmise au dépositaire ; vous pouvez la copier dans une nouvelle commande."),
            "es" => new PreOrderMail("Rechazado · Prepedido {{orderCode}} · {{pharmacyName}}", "Su prepedido fue rechazado",
                "El aprobador rechazó el prepedido de {{pharmacyName}}. No se enviará al distribuidor; puede copiarlo en un pedido nuevo."),
            "zh" => new PreOrderMail("已拒绝 · 预订单 {{orderCode}} · {{pharmacyName}}", "您的预订单已被拒绝",
                "审批人拒绝了 {{pharmacyName}} 的预订单。该订单不会发送给经销商；您可以复制它创建新订单。"),
            "ar" => new PreOrderMail("مرفوض · طلب مسبق {{orderCode}} · {{pharmacyName}}", "تم رفض طلبك المسبق",
                "رفض المعتمِد الطلب المسبق لصيدلية {{pharmacyName}}. لن يُرسل إلى المستودع، ويمكنك نسخه إلى طلب جديد."),
            "ru" => new PreOrderMail("Отклонено · Предзаказ {{orderCode}} · {{pharmacyName}}", "Ваш предзаказ отклонён",
                "Согласующий отклонил предзаказ для аптеки {{pharmacyName}}. Он не будет передан дистрибьютору; его можно скопировать в новый заказ."),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderRejectedEvent, locale, m with { Action = w.OpenOrder },
            [(w.Reason, "{{reason}}"), (w.Pharmacy, "{{pharmacyName}}"), (w.Lines, "{{lineCount}}")],
            actionUrlVariable: "orderUrl", footnote: w.FootInternal, required: ["reason"]);
    }

    /// <summary><c>commercial.preorder.transmitteddepot</c> — to the depot (external): the approved order, the depot copy PDF
    /// attached from W6-BE-c2. No CRM link; no effective discount anywhere (DESIGN-GAP §3).</summary>
    private static NotificationTemplate PreOrderTransmittedDepot(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Pre-order {{orderCode}} · {{pharmacyName}} · delivery {{deliveryDate}}", "Pre-order {{orderCode}}",
                "Please deliver the pharmacy order below on the date given. The depot copy is attached."),
            "tr" => new PreOrderMail("Ön sipariş {{orderCode}} · {{pharmacyName}} · teslim {{deliveryDate}}", "Ön sipariş {{orderCode}}",
                "Aşağıdaki eczane siparişinin belirtilen tarihte teslimini rica ederiz. Depo nüshası ektedir."),
            "fr" => new PreOrderMail("Pré-commande {{orderCode}} · {{pharmacyName}} · livraison {{deliveryDate}}", "Pré-commande {{orderCode}}",
                "Merci de livrer la commande de la pharmacie ci-dessous à la date indiquée. L'exemplaire du dépositaire est joint."),
            "es" => new PreOrderMail("Prepedido {{orderCode}} · {{pharmacyName}} · entrega {{deliveryDate}}", "Prepedido {{orderCode}}",
                "Le rogamos que entregue el pedido de la farmacia indicado abajo en la fecha señalada. Se adjunta la copia del distribuidor."),
            "zh" => new PreOrderMail("预订单 {{orderCode}} · {{pharmacyName}} · 交货 {{deliveryDate}}", "预订单 {{orderCode}}",
                "请于指定日期交付以下药店订单。随附经销商联。"),
            "ar" => new PreOrderMail("طلب مسبق {{orderCode}} · {{pharmacyName}} · التسليم {{deliveryDate}}", "طلب مسبق {{orderCode}}",
                "نرجو تسليم طلب الصيدلية أدناه في التاريخ المحدد. نسخة المستودع مرفقة."),
            "ru" => new PreOrderMail("Предзаказ {{orderCode}} · {{pharmacyName}} · доставка {{deliveryDate}}", "Предзаказ {{orderCode}}",
                "Просим доставить заказ аптеки, указанный ниже, в указанную дату. Экземпляр дистрибьютора приложен."),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderTransmittedDepotEvent, locale, m,
            [(w.OrderNo, "{{orderCode}}"), (w.Pharmacy, "{{pharmacyName}}"), (w.Delivery, "{{deliveryDate}}"), (w.Lines, "{{lineCount}}"),
             (w.Total, "{{totalText}}"), (w.Note, "{{note}}")],
            actionUrlVariable: null, footnote: w.FootExternal);
    }

    /// <summary><c>commercial.preorder.pharmacycopy</c> — to the pharmacy (external): the order was approved and passed to
    /// the depot; the pharmacy copy PDF attached from W6-BE-c2.</summary>
    private static NotificationTemplate PreOrderPharmacyCopy(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Your order was received · {{orderCode}} · {{pharmacyName}}", "Your order was received",
                "The order you placed with our representative was approved and passed to {{depotName}}. Your copy is attached."),
            "tr" => new PreOrderMail("Siparişiniz alındı · {{orderCode}} · {{pharmacyName}}", "Siparişiniz alındı",
                "Temsilcimizle oluşturduğunuz sipariş onaylandı ve {{depotName}} deposuna iletildi. Kopyanız ektedir."),
            "fr" => new PreOrderMail("Votre commande a bien été reçue · {{orderCode}} · {{pharmacyName}}", "Votre commande a bien été reçue",
                "La commande passée avec notre délégué a été validée et transmise à {{depotName}}. Votre exemplaire est joint."),
            "es" => new PreOrderMail("Hemos recibido su pedido · {{orderCode}} · {{pharmacyName}}", "Hemos recibido su pedido",
                "El pedido que realizó con nuestro representante fue aprobado y enviado a {{depotName}}. Se adjunta su copia."),
            "zh" => new PreOrderMail("您的订单已收到 · {{orderCode}} · {{pharmacyName}}", "您的订单已收到",
                "您通过我们代表下的订单已审批通过，并已发送给 {{depotName}}。随附您的副本。"),
            "ar" => new PreOrderMail("تم استلام طلبك · {{orderCode}} · {{pharmacyName}}", "تم استلام طلبك",
                "تمت الموافقة على الطلب الذي أنشأته مع مندوبنا وأُرسل إلى {{depotName}}. نسختك مرفقة."),
            "ru" => new PreOrderMail("Ваш заказ получен · {{orderCode}} · {{pharmacyName}}", "Ваш заказ получен",
                "Заказ, оформленный с нашим представителем, согласован и передан дистрибьютору {{depotName}}. Ваш экземпляр приложен."),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderPharmacyCopyEvent, locale, m,
            [(w.OrderNo, "{{orderCode}}"), (w.Depot, "{{depotName}}"), (w.Delivery, "{{deliveryDate}}"), (w.Lines, "{{lineCount}}")],
            actionUrlVariable: null, footnote: w.FootExternal);
    }

    /// <summary><c>commercial.preorder.cancelled</c> — to the rep and the manager, and (only after a transmission) to the depot
    /// and the pharmacy (D5, AC-FLOW-8). One template: CRM leaves orderUrl empty for the external parts.</summary>
    private static NotificationTemplate PreOrderCancelled(string locale)
    {
        var w = PreOrderWordsFor(locale);
        var m = locale switch
        {
            "en" => new PreOrderMail("Cancelled · Pre-order {{orderCode}} · {{pharmacyName}}", "The pre-order was cancelled",
                "Pre-order {{orderCode}} for {{pharmacyName}} was cancelled. Please do not deliver or invoice it."),
            "tr" => new PreOrderMail("İptal edildi · Ön sipariş {{orderCode}} · {{pharmacyName}}", "Ön sipariş iptal edildi",
                "{{pharmacyName}} için {{orderCode}} numaralı ön sipariş iptal edildi. Lütfen teslim etmeyin, faturalamayın."),
            "fr" => new PreOrderMail("Annulée · Pré-commande {{orderCode}} · {{pharmacyName}}", "La pré-commande a été annulée",
                "La pré-commande {{orderCode}} pour {{pharmacyName}} a été annulée. Merci de ne pas la livrer ni la facturer."),
            "es" => new PreOrderMail("Cancelado · Prepedido {{orderCode}} · {{pharmacyName}}", "El prepedido se ha cancelado",
                "El prepedido {{orderCode}} de {{pharmacyName}} se ha cancelado. Por favor, no lo entregue ni lo facture."),
            "zh" => new PreOrderMail("已取消 · 预订单 {{orderCode}} · {{pharmacyName}}", "预订单已取消",
                "{{pharmacyName}} 的预订单 {{orderCode}} 已取消。请勿交货或开具发票。"),
            "ar" => new PreOrderMail("أُلغي · طلب مسبق {{orderCode}} · {{pharmacyName}}", "تم إلغاء الطلب المسبق",
                "تم إلغاء الطلب المسبق {{orderCode}} لصيدلية {{pharmacyName}}. يُرجى عدم تسليمه أو إصدار فاتورة له."),
            "ru" => new PreOrderMail("Отменено · Предзаказ {{orderCode}} · {{pharmacyName}}", "Предзаказ отменён",
                "Предзаказ {{orderCode}} для аптеки {{pharmacyName}} отменён. Пожалуйста, не доставляйте его и не выставляйте счёт."),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported pre-order template locale.")
        };

        return PreOrderTemplate(PreOrderEvents.PreOrderCancelledEvent, locale, m with { Action = w.OpenOrder },
            [(w.Reason, "{{reason}}"), (w.OrderNo, "{{orderCode}}"), (w.Pharmacy, "{{pharmacyName}}"), (w.Depot, "{{depotName}}")],
            // the external parts get no link: the plain-text part writes none either
            actionUrlVariable: "orderUrl", footnote: w.FootInternal, required: ["reason"], linkInText: false);
    }

    private static NotificationTemplate PreOrderTemplate(
        string templateKey,
        string locale,
        PreOrderMail mail,
        IReadOnlyList<(string Label, string Value)> rows,
        string? actionUrlVariable,
        string footnote,
        IReadOnlyList<string>? required = null,
        bool linkInText = true)
    {
        var text = mail.Notice is null ? mail.Intro : $"{mail.Intro} {mail.Notice}";
        var template = Create(
            templateKey,
            locale,
            mail.Subject,
            mail.Notice is null ? $"<p>{mail.Intro}</p>" : $"<p>{mail.Intro}</p><p><strong>{mail.Notice}</strong></p>",
            // The plain-text part writes the link out only for the internal mails; an external one has none to write.
            actionUrlVariable is null || mail.Action is null || !linkInText ? text : $"{text} {mail.Action}: {{{{{actionUrlVariable}}}}}",
            PreOrderEvents.PreOrderRequiredVariables.Concat(required ?? []).ToList());
        template.Shell = new NotificationTemplateShell
        {
            HeadingTemplate = mail.Heading,
            InfoRows = rows.Select(row => new NotificationTemplateShellRow { Label = row.Label, ValueTemplate = row.Value }).ToList(),
            ActionLabel = actionUrlVariable is null ? null : mail.Action,
            ActionUrlVariable = actionUrlVariable,
            FootnoteTemplate = footnote
        };
        return template;
    }
}
