import os

content = '''PLAN DE TESTARE
Aplicația AppClubCalculatoare


Autor: Dudnic Alexandru
Produs testat: AppClubCalculatoare, versiunea 1.0.0.0
Data: 16.10.2026


Cuprins
Introducere                                                            3
Capitolul 1. Obiectivele testării                                    3
1.1. Obiectivele generale                                           3
1.2. Obiective măsurabile                                           3
Capitolul 2. Domeniul de aplicare                                    4
2.1. Ce intră în domeniul de aplicare                                 4
2.2. Ce NU intră în domeniul de aplicare                              4
Capitolul 3. Strategia de testare                                     4
Capitolul 4. Ipoteza                                                  4
Capitolul 5. Roluri și responsabilități                              5
Capitolul 6. Mediul de testare                                        5
6.1. Configurația mediului                                          5
6.2. Etape de pregătire                                              5
6.3. Conturi și date de test                                          6
Capitolul 7. Gestionarea defectelor                                    6
7.1. Clasificarea severității                                        6
7.2. Șablon de raportare a unui defect                                6
Capitolul 8. Criterii de intrare și ieșire                              6
8.1. Criterii de intrare                                              6
8.2. Criterii de ieșire                                             7
Capitolul 9. Rezultatul testării                                       7
9.1. Sinteza rezultatelor                                            7
9.2. Rezultatele pe module                                            7
9.3. Defecte / observații identificate                               11
9.4. Concluzie                                                      12


Introducere
Acest document reprezintă planul de testare întocmit pentru aplicația AppClubCalculatoare. Aplicația fostă dezvoltată în limbajul C# cu interfață vizuală tip WinForms, pe platforma .NET 8, și se conectează la bază de date Microsoft SQL Server Express (instanță SQLEXPRESS), bază de date numită ClubCalculatoare. Aplicația oferă funcționalități pentru gestionarea unui club de calculatoare, acoperind următoarele module: membri, săli, echipamente, sesiuni, rezervări, operatori și încasări.
Testarea a fost efectuată în mod etnic, aplicând toate fluxurile principale ale aplicației și verificând în mod direct rezultatele în baza de date. Pentru fiecare pas important au fost efectuate capturi de ecran și verificate rezultatele folosind instrumente de acces la bază de date.
Documentul următor conține: obiectivele testării, domeniul de aplicare, strategia de testare, ipoteza, rolurile și responsabilitățile, mediul de testare, metoda de gestionare a defectelor, criteriile de intrare și iesire, și rezultatele obținute, cu dovezi concrete (tabele și imagini).


Capitolul 1. Obiectivele testării
1.1. Obiectivele generale
Scopul testării a fost verificarea corectitudinii funcționării aplicației în fluxurile sale celemai importante și validarea corectitudinii transferului datelor introduse din interfață în baza de date. Testarea nu s-a limitat la activarea butoanelor, ci a implicat compararea între cele vizibile pe ecran și cele scrie în tabelele bazei de date.
1.2. Obiective măsurabile
Obiectivele au fost formulat cu cifre și praguri clare pentru a permite verificarea asupra lor:


Capitolul 2. Domeniul de aplicare
2.1. Ce intră în domeniul de aplicare
Au fost testate următoarele module și funcționalități:
• Autentificare (ca și admin) și deconectare;
• Tabla de bord cu indicatori (KPI) și avertismente;
• Operații CRUD (creare, citire, actualizare, ștergere) pentru Echipamente, Sali, Membri, Sesiuni, Rezervări și Operatori;
• Căutare și filtrare în liste;
• Validarea datelor introduse în formulare;
• Rapoarte și statistici, cu export în format CSV și previzualizare tipărire;
• Gestionarea utilizatorilor, calculatorul de cost, profilul și schimbarea parolei.
Tipurile de testare folosite: testare funcțională, testare de validare, testare de integrare cu baza de date, testare pe roluri și testare de regresie pe fluxurile principale. Platforma vizată: Windows 11 x64, cu .NET 8 și SQL Server Express.
2.2. Ce NU intră în domeniul de aplicare
• Testarea de performanță și încărcare (aplicația este desktop, utilizată local)
• Testarea de securitate avansată (tip penetration testing);
• Testarea pe alte sisteme de operare în afară de Windows;
• Modulele care nu au fost implementate în această versiune.


Capitolul 3. Strategia de testare
A fost aplicată o strategie de testare manuală, funcțională și bazată pe risc. Astfel, fluxurile cele mai utilizate (autentificarea, adăugarea și ștergerea de date, rapoartele) au fost verify cu atenție, deoarece acolo riscul de a afecta date este cel mai mare.
Strategia, pe scurt:
Tehnici: cazuri pozitive (date corecte) și negative (date lipsă sau invalide), verificare încrucișată a interfeței cu baza de date;
Niveluri: testare de sistem (cap-coadă) și testare de integrare (aplicație – bază de date);
Ordinea de executie: autentificare → tablou de bord → CRUD pe fiecare modul → rapoarte și export → controlul accesului pe roluri → deconectare;
Mijloace: testare manuală cu mouse și tastatură, capturi de ecran la fiecare pas, verificări SQL cu sqlcmd;
Mediu: un singur mediu local, cu date demonstrative pregătite dinainte.


Capitolul 4. Ipoteza
S-a plecat de la ipoteza că, în versiunea 1.0.0.0, funcțiile de bază ale aplicației AppClubCalculatoare funcționează corect și că datele salvate din interfață ajung corect în baza de date. Se estimează ca eventualele probleme să fie, de obicei, de aspect vizual sau de validare, nu blocante, deoarece defectele majore au fost deja corectate în etape anterioare.
Practic, dacă ipoteza este corectă, atunci: se poate autentifica cu ambele conturi, se pot adăuga și șterge înregistrării fără erori, datele apar imediat în bază de date și utilizatorul cu rol restrâns nu poate modifica nimic. Aceste aspecte au fost urmărite în timpul testării.


Capitolul 5. Roluri și responsabilități
Pentru acest proiect, rolurile au fost împărțite astfel:
• Dudnic Alexandru – autor și realizator al testelor;
• Constantin – utilizator de test cu rol administrator;
• Maxim – utilizator de test cu rol utilizator obișnuit.


Capitolul 6. Mediul de testare
6.1. Configurația mediului
• Sistem de operare: Windows 11 x64;
• Limbaj de programare: C#;
• Platformă: .NET 8;
• Interfață: WinForms;
• Bază de date: Microsoft SQL Server Express (instanță SQLEXPRESS);
• Baza de date: ClubCalculatoare;

6.2. Etape de pregătire
• Proiectul a fost compilat în configurația Debug (fără erori);
• Serviciul SQL Server Express a fost pornit;
• Scripturile bazei de date au fost rulate și au fost încărcate datele demonstrative;
• Aplicația AppClubCalculatoare.exe a fost lansată și autentificat cu contul de administrator;
• A fost creat folderul pentru dovezi (capturi de ecran).

6.3. Conturi și date de test
Pentru verificarea rolurilor și a restricțiilor de acces au fost utilizate două conturi:
Administrator: Constantin / 121212;
Utilizator: Maxim / 575757.
Datele demonstrative din baza de date au fost: 10 operatori, 10 săli, 10 echipamente, 10 membri, 40 de sesiuni și 13 rezervări.


Capitolul 7. Gestionarea defectelor
A fost stabilit un mod simplu și clar de tratament al defectelor, pentru a nu se piardă niciunul în proces:
Identificare — observerea comportamentului neașteptat în timpul testării;
Înregistrare — notarea unui ID, titlu, severitate, pași de reproducere, comportamentul real și așteptat, plus dovada (captură de ecran);
Triaj — stabilirea severității și priorității;
Remediere — corectarea problemei de către dezvoltator;
Retestare — reluarea testului pe versiunea corectată;
Închidere — închiderea defectului dacă testul trece.
7.1. Clasificarea severității
• Critic – influență asupra funcționalităților principale, blocantă;
• Mare – afectează funcționalități importante, dar există soluții;
• Mică – probleme minore, de aspect vizual;

7.2. Șablon de raportare a unui defect
• ID defect;
• Titlu;
• Descriere;
• Severitate;
• Prioritate;
• Pași de reproducere;
• Comportament așteptat;
• Comportament observat;
• Dovadă (screenshot);
• Stare;
• Responsabil;
• Data și ora.


Capitolul 8. Criterii de intrare și ieșire
8.1. Criterii de intrare
Pentru ca testarea să poată începe, trebuie să fie îndeplinite următoarele condiții:
• Proiectul este compilat fără erori în configurația Debug;
• Serviciul SQL Server Express este pornit;
• Baza de date ClubCalculatoare există și conține datele demonstrative;
• Cele două conturi de test (Admin și User) sunt active;
• Executabilul AppClubCalculatoare.exe pornește și afișează fereastra de autentificare.
8.2. Criterii de ieșire
Testarea este considerată încheiată dacă sunt îndeplinite următoarele condiții:
• Toate cazurile de test planificate au fost executate (acoperire 100% pe fluxurile principale);
• Toate cazurile de test au rezultatul Pas, sau defectele găsite au fost înregistrate și corectate;
• Nu există defecte de severitate Critică deschise;
• Dovezile (capturi de ecran și verificări în bază de date) sunt atașate pentru fiecare modul.


Capitolul 9. Rezultatul testării
9.1. Sinteza rezultatelor
Testarea a fost efectuată manual, cu mouse și tastatură, obținând următoarele rezultate concrete:

9.2. Rezultatele pe module
Autentificare și tablou de bord — Autentificarea s-a realizat în calitate de administrator; Tabla de bord a afișat corect: Echipamente libere 5, Sesiuni în așteptare 2, Rezervări programate 5, Încasare octombrie 2026 – 1.099 EUR, Încasare totală – 12.668 EUR, Valoare portofoliu – 848 EUR, top 3 operatori și 6 avertismente active.

Echipamente (CRUD) — Lista a afișat 10 înregistrări; căutarea "PC" a returnat 4 înregistrări. La adăugarea unei înregistrări de test (TEST-PC-01), s-a verificat în bază de date că a fost inserată și că s-a scris în AuditLog. La ștergerea unei înregistrări cu dependențe (PC-01), aplicația a blocat operația cu mesajul "Acest echipament are 9 sesiuni sau rezervări."

Celelalte liste — Sali (10 înregistrări), Membri (10), Sesiuni (40), Rezervări (13) și Operatori (10) — s-au deschis corect și au afișat toate coloanele.

Rapoarte și export — Raportul s-a generat corect (40 de înregistrări în previzualizare tipărire), iar la export s-a obținut un fișier CSV cu un singur BOM și separatorul ";".

Calculator de cost — Pentru 30 EUR/oră, 120 de minute și 10 sesiuni, cu reducere de membru 10%, aplicația a calculat corect: cost brut 600,00 EUR, reducere –60,00 EUR, total de plată 540,00 EUR. Toate valorile sunt afișate în EUR.

Controlul accesului — Autentificat ca utilizator simplu (Maxim), aplicația a afișat meniu restrâns și fereastra "Gestionare Echipamente [doar vizualizare]", fără butoane de adăugare, modificare sau ștergere.

9.3. Defecte / observații identificate
Pe lângă rezultatele pozitive, au fost observate câteva probleme, toate de severitate mică sau medie (nici una blocantă):
• Culoarea textului de pe rândul selectat necesită corectare;
• Forțarea limbii române în dialogurile standard de salvare;
• Corectarea caracterului "ș" din statusuri;
• Configurarea funcției Alt+F4 pentru închiderea ferestrei de previzualizare.

9.4. Concluzie
În urma testării, se consideră că ipoteza de la Capitolul 4 s-a confirmat: funcțiile de bază ale aplicației AppClubCalculatoare funcționează corect, datele introduse din interfață ajung corect în bază de date, iar controlul accesului pe roluri este respectat. Toate cele 32 de cazuri de test au trecut, iar singurile probleme găsite sunt de aspect vizual și de localizare, de severitate mică și medie.
Recomandările sunt următoare: după corectarea acestor probleme minore, aplicația poate fi considerată pe deplin aptă de utilizare.
'''

with open('Plan_de_Testare_corectat.txt', 'w', encoding='utf-8') as f:
    f.write(content)
print('Documentul corectat a fost creat cu succes!')