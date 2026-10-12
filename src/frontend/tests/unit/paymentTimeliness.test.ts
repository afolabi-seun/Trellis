import { describe, expect, it } from "vitest";
import {
  describeTimeliness,
  timelinessPercent,
} from "../../src/features/admin/rentledger/paymentTimeliness";

// BDR-RENT-007: "no data" is never shown as a measured 0% or 100%.
describe("payment timeliness wording", () => {
  it("says nothing is assessed yet rather than inventing a percentage", () => {
    const none = { periodsAssessed: 0, periodsOnTime: 0, onTimeRate: null, averageDaysLate: null };
    expect(describeTimeliness(none)).toBe("No periods past grace yet");
    expect(timelinessPercent(none)).toBe("—");
  });

  it("gives the count, the rate, and days late only when something was late", () => {
    expect(
      describeTimeliness({
        periodsAssessed: 4,
        periodsOnTime: 3,
        onTimeRate: 0.75,
        averageDaysLate: 6,
      }),
    ).toBe("3 of 4 periods paid on time (75%) · late periods averaged 6 days late");
    expect(
      describeTimeliness({
        periodsAssessed: 2,
        periodsOnTime: 2,
        onTimeRate: 1,
        averageDaysLate: null,
      }),
    ).toBe("2 of 2 periods paid on time (100%)");
    expect(
      timelinessPercent({
        periodsAssessed: 3,
        periodsOnTime: 2,
        onTimeRate: 0.6667,
        averageDaysLate: 4,
      }),
    ).toBe("67%");
  });
});
