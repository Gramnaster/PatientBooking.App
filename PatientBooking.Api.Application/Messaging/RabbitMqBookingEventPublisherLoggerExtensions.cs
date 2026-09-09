using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PatientBooking.Api.Application.Messaging;

internal static partial class RabbitMqBookingEventPublisherLoggerExtensions
{
    [LoggerMessage(EventId = 3202, Level = LogLevel.Warning, Message = "Failed to publish booking-confirmed event for booking {BookingId} - no channel available.")]
    public static partial void BookingConfirmedPublishSkipped(this ILogger logger, int bookingId);

    [LoggerMessage(EventId = 3203, Level = LogLevel.Warning, Message = "Failed to publish booking-confirmed event for booking {BookingId}.")]
    public static partial void BookingConfirmedPublishFailed(this ILogger logger, Exception exception, int bookingId);
}
