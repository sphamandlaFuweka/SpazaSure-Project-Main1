import { Link, useSearchParams } from 'react-router-dom';
import { ShieldCheck } from 'lucide-react';

/*
 * DRAFT privacy notice (POPIA s18). Have it reviewed by someone with legal expertise before launch.
 * Fill in the company details below, and change PRIVACY_NOTICE_VERSION here and
 * PrivacyNotice.Version in the backend (SpazaSure.Shared/Helpers/PrivacyNotice.cs) whenever the
 * wording changes, so recorded consents show which version people agreed to.
 */
const PRIVACY_NOTICE_VERSION = '2026-10-20-draft';
const COMPANY = {
  legalName: '[Registered company name – to be confirmed]',
  registrationNumber: '[CIPC registration number – to be confirmed]',
  address: '[Physical address – to be confirmed]',
  informationOfficer: '[Information Officer name – to be confirmed]',
  email: '[privacy@spazasure.co.za – to be confirmed]',
  phone: '[Phone number – to be confirmed]',
};

const sections: { title: string; body: (string | string[])[] }[] = [
  {
    title: 'Who we are',
    body: [
      `SpazaSure is operated by ${COMPANY.legalName} (${COMPANY.registrationNumber}), ${COMPANY.address}. We are the responsible party for the personal information described here.`,
    ],
  },
  {
    title: 'What we collect',
    body: [[
      'Account details: name, phone number, email address and password (stored only as a secure hash).',
      'Shop owners: South African ID number, shop name and address, map location, business documents (permit, health certificate) and registration details you provide.',
      'Suppliers: company name, CIPC registration and VAT numbers, contact person, business address, banking details for payouts, and company documents.',
      'Customers: name, age, area, optional location, preferences, product scans and reports you send.',
      'Identity checks: a photo of your ID document and a selfie, and the results of checks against the Department of Home Affairs, the CIPC register and sanctions lists.',
      'Orders, payments, wallet transactions, chat messages and reviews.',
      'Technical information: IP address and sign-in activity, used for security.',
    ]],
  },
  {
    title: 'Why we use it',
    body: [[
      'To create and run your account, and to process orders, payments and payouts.',
      'To confirm shop owners’ identities and suppliers’ businesses, so shops can earn the SpazaSure Verified sticker and buyers can trust suppliers.',
      'To keep the platform safe: preventing fraud, counterfeit goods and misuse.',
      'To meet legal obligations, such as keeping financial records.',
      'To send you marketing, only if you agreed to it. You can stop it at any time.',
    ]],
  },
  {
    title: 'Who we share it with',
    body: [[
      'Didit (didit.me), our identity verification partner, which checks ID documents, selfies, Home Affairs and CIPC records and sanctions lists. Didit processes this information outside South Africa, under a written agreement that protects it as POPIA requires (section 72).',
      'Paystack, which processes card payments.',
      'Suppliers and shops you trade with see the details needed to complete an order, such as the shop name and delivery address.',
      'Authorities, when the law requires it.',
    ], 'We never sell your personal information.'],
  },
  {
    title: 'How we protect it',
    body: [
      'Personal information is sent over encrypted connections. Identity and business documents are private: they can only be opened through short-lived links by you and SpazaSure staff who review them. Staff access is limited to the areas they work in.',
    ],
  },
  {
    title: 'How long we keep it',
    body: [
      'We keep your information while your account is open. When you delete your account, we erase your personal details and documents. We keep order, payment and wallet records for five years without your personal details, because tax law requires it. When you replace a document, the old copy is deleted.',
    ],
  },
  {
    title: 'Your rights',
    body: [[
      'See the information we hold about you: use “Download my data” in your profile.',
      'Correct it: edit your profile, or contact us.',
      'Delete it: use “Delete my account” in your profile, or the form at portal.spazasure.co.za/delete-account. We delete it within 30 days, unless something still needs settling, like an unpaid order or money in your wallet.',
      'Object to processing or withdraw consent, including for marketing.',
      `Complain to the Information Regulator (inforegulator.org.za, complaints.IR@inforegulator.org.za) if you are not happy with how we handle your information. Please contact us first so we can try to help.`,
    ]],
  },
  {
    title: 'Contact our Information Officer',
    body: [`${COMPANY.informationOfficer} · ${COMPANY.email} · ${COMPANY.phone} · ${COMPANY.address}`],
  },
];

export default function PrivacyNoticePage() {
  // Opened from the SpazaSure app (?app=1): the app closes the page when this link is followed
  const [params] = useSearchParams();
  const fromApp = params.get('app') === '1';
  return (
    <div className="min-h-screen bg-gray-50 px-4 py-10">
      <article className="mx-auto max-w-3xl rounded-3xl bg-white p-6 sm:p-10 shadow-sm border border-gray-100">
        <div className="flex items-center gap-3 mb-2">
          <ShieldCheck className="text-primary" size={28} />
          <h1 className="text-2xl font-black text-gray-900">SpazaSure privacy notice</h1>
        </div>
        <p className="text-sm text-gray-500 mb-8">
          How SpazaSure handles your personal information under the Protection of Personal Information Act (POPIA).
          Version {PRIVACY_NOTICE_VERSION}.
        </p>

        {sections.map((section) => (
          <section key={section.title} className="mb-7">
            <h2 className="text-lg font-bold text-gray-900 mb-2">{section.title}</h2>
            {section.body.map((part, i) =>
              Array.isArray(part) ? (
                <ul key={i} className="list-disc pl-5 space-y-1.5 text-sm text-gray-700">
                  {part.map((item) => <li key={item}>{item}</li>)}
                </ul>
              ) : (
                <p key={i} className="text-sm text-gray-700 leading-relaxed mt-2">{part}</p>
              ),
            )}
          </section>
        ))}

        <Link to="/login" className="text-sm font-semibold text-primary hover:underline">
          {fromApp ? 'Back to the SpazaSure app' : 'Back to SpazaSure'}
        </Link>
      </article>
    </div>
  );
}
