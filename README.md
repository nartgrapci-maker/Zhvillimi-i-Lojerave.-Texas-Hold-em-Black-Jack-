# Texas Hold'em (deri në 4 lojtarë)

**Lënda:** PBT 1, Java 2 - Historiku i lojërave
**Faza:** I - Zbulimi dhe përcaktimi i projektit

## Anëtarët e ekipit

- Nart Grapci - Studenti A
- Enis Kraja - Studenti B
- Arlind Hyseni - Studenti C

## Ideja e shkurtër e lojës

Texas Hold'em është një lojë pokeri ku një lojtar njerëzor luan kundër 1 deri 3 kundërshtarëve kompjuterikë (maksimumi 4 lojtarë në tavolinë). Çdo lojtar merr 2 letra private, ndërsa 5 letra të përbashkëta shpërndahen gradualisht (flop, turn, river). Në çdo raund bastesh lojtari mund të bëjë check, call, raise ose fold. Fiton ai që mbetet me chips-et e të gjithë kundërshtarëve.

## Qëllimi i projektit

Loja e shndërron në gameplay problemin e vendimmarrjes me informacion të paplotë: lojtari nuk i di letrat e kundërshtarëve dhe duhet të lidhë probabilitetin, madhësinë e potit dhe rrezikun për të vendosur.

Komponentët logjikë dhe algoritmikë të projektit:

- vlerësuesi i duarve (dora më e mirë prej 5 letrave nga 7)
- simulim Monte Carlo për gjasat e fitores
- vlera e pritur dhe pot odds për vendimet
