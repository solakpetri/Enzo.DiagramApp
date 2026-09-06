# Enzo vs Mermaid Token Benchmark

Scenarios: 30
Encoding: `cl100k_base`

| Metric | Enzo | Mermaid |
| --- | ---: | ---: |
| Total tokens | 4465 | 4410 |
| Average tokens | 148.8 | 147 |
| Median tokens | 119 | 119 |
| Characters | 18453 | 17052 |
| UTF-8 bytes | 18453 | 17052 |
| Non-empty lines | 733 | 733 |

Overall token difference: Enzo uses 1.2% more tokens than Mermaid.

## By Category

| Group | Scenarios | Enzo tokens | Mermaid tokens | Difference |
| --- | ---: | ---: | ---: | --- |
| flow | 10 | 1831 | 1720 | Enzo uses 6.5% more tokens than Mermaid. |
| process | 10 | 1372 | 1306 | Enzo uses 5.1% more tokens than Mermaid. |
| sequence | 10 | 1262 | 1384 | Enzo uses 8.8% fewer tokens than Mermaid. |

## By Complexity

| Group | Scenarios | Enzo tokens | Mermaid tokens | Difference |
| --- | ---: | ---: | ---: | --- |
| complex | 12 | 2812 | 2781 | Enzo uses 1.1% more tokens than Mermaid. |
| medium | 9 | 1017 | 1003 | Enzo uses 1.4% more tokens than Mermaid. |
| simple | 9 | 636 | 626 | Enzo uses 1.6% more tokens than Mermaid. |

## Scaling Samples

| Scenario | Elements | Connections | Enzo tokens | Mermaid tokens |
| --- | ---: | ---: | ---: | ---: |
| sequence-cache-lookup | 4 | 6 | 58 | 63 |
| sequence-login-basic | 4 | 6 | 63 | 68 |
| sequence-notification-basic | 4 | 6 | 56 | 61 |
| flow-content-review | 6 | 6 | 78 | 73 |
| flow-login-basic | 6 | 5 | 64 | 59 |
| flow-support-ticket | 6 | 6 | 83 | 79 |
| process-expense-basic | 6 | 6 | 71 | 68 |
| sequence-device-provisioning | 6 | 10 | 115 | 128 |
| sequence-order-checkout | 6 | 10 | 100 | 109 |
| sequence-report-export | 6 | 10 | 98 | 107 |
| process-customer-refund | 7 | 7 | 82 | 78 |
| process-vacation-request | 7 | 7 | 81 | 77 |
| flow-order-fulfillment | 8 | 9 | 99 | 92 |
| sequence-claims-processing | 8 | 14 | 139 | 152 |
| sequence-release-orchestration | 8 | 18 | 171 | 187 |
| sequence-travel-booking | 8 | 19 | 182 | 201 |
| flow-password-reset | 9 | 9 | 108 | 99 |
| process-procurement | 9 | 10 | 107 | 102 |
| flow-invoice-approval | 10 | 10 | 123 | 114 |
| sequence-large-onboarding | 10 | 28 | 280 | 308 |
| process-contract-review | 11 | 12 | 131 | 124 |
| process-customer-onboarding | 11 | 12 | 136 | 128 |
| flow-data-import | 14 | 17 | 181 | 171 |
| process-healthcare-referral | 14 | 16 | 187 | 180 |
| process-incident-management | 14 | 16 | 199 | 190 |
| process-manufacturing-quality | 14 | 16 | 194 | 184 |
| process-subscription-lifecycle | 14 | 17 | 184 | 175 |
| flow-loan-application | 17 | 21 | 250 | 238 |
| flow-release-pipeline | 18 | 22 | 245 | 233 |
| flow-warehouse-wave | 42 | 45 | 600 | 562 |