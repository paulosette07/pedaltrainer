@{
    # Metadados dos circuitos usados para gerar os traçados embutidos no aplicativo.
    # Lat/Lon são o centro aproximado do traçado e Radius a busca no OpenStreetMap.
    Circuits = @(
        @{ Id='interlagos'; Name='Autódromo José Carlos Pace'; City='São Paulo'; Country='Brasil'; Code='BR'; Length=4309; Lat=-23.7036; Lon=-46.6997; Radius=1500 }
        @{ Id='goiania'; Name='Autódromo Internacional Ayrton Senna'; City='Goiânia'; Country='Brasil'; Code='BR'; Length=3800; Lat=-16.7167; Lon=-49.1933; Radius=1600 }
        @{ Id='velo-citta'; Name='Autódromo Velo Città'; City='Mogi Guaçu'; Country='Brasil'; Code='BR'; Length=3560; Lat=-22.3900; Lon=-46.9200; Radius=1600 }
        @{ Id='cascavel'; Name='Autódromo Zilmar Beux'; City='Cascavel'; Country='Brasil'; Code='BR'; Length=3075; Lat=-24.9833; Lon=-53.4333; Radius=1600 }
        @{ Id='curitiba'; Name='Autódromo Internacional de Curitiba'; City='Pinhais'; Country='Brasil'; Code='BR'; Length=3845; Lat=-25.4167; Lon=-49.1833; Radius=1600 }
        @{ Id='taruma'; Name='Autódromo Internacional de Tarumã'; City='Viamão'; Country='Brasil'; Code='BR'; Length=3060; Lat=-30.0500; Lon=-51.0333; Radius=1600 }
        @{ Id='monza'; Name='Autodromo Nazionale di Monza'; City='Monza'; Country='Itália'; Code='IT'; Length=5793; Lat=45.6156; Lon=9.2811; Radius=1800 }
        @{ Id='imola'; Name='Autodromo Enzo e Dino Ferrari'; City='Imola'; Country='Itália'; Code='IT'; Length=4909; Lat=44.3439; Lon=11.7167; Radius=1800 }
        @{ Id='mugello'; Name='Autodromo Internazionale del Mugello'; City='Scarperia'; Country='Itália'; Code='IT'; Length=5245; Lat=43.9969; Lon=11.3719; Radius=1800 }
        @{ Id='misano'; Name='Misano World Circuit Marco Simoncelli'; City='Misano Adriatico'; Country='Itália'; Code='IT'; Length=4226; Lat=43.9750; Lon=12.6833; Radius=1500 }
        @{ Id='vallelunga'; Name='Autodromo Piero Taruffi di Vallelunga'; City='Campagnano'; Country='Itália'; Code='IT'; Length=4085; Lat=42.1500; Lon=12.2333; Radius=1600 }
        @{ Id='spa'; Name='Circuit de Spa-Francorchamps'; City='Stavelot'; Country='Bélgica'; Code='BE'; Length=7004; Lat=50.4372; Lon=5.9714; Radius=2500 }
        @{ Id='zolder'; Name='Circuit Zolder'; City='Heusden-Zolder'; Country='Bélgica'; Code='BE'; Length=4011; Lat=50.9897; Lon=5.2547; Radius=1600 }
        @{ Id='zandvoort'; Name='Circuit Zandvoort'; City='Zandvoort'; Country='Países Baixos'; Code='NL'; Length=4252; Lat=52.3888; Lon=4.5400; Radius=1600 }
        @{ Id='assen'; Name='TT Circuit Assen'; City='Assen'; Country='Países Baixos'; Code='NL'; Length=4542; Lat=52.9600; Lon=6.5236; Radius=1600 }
        @{ Id='nurburgring-gp'; Name='Nürburgring Grand-Prix-Strecke'; City='Nürburg'; Country='Alemanha'; Code='DE'; Length=5148; Lat=50.3356; Lon=6.9475; Radius=1600 }
        @{ Id='hockenheim'; Name='Hockenheimring'; City='Hockenheim'; Country='Alemanha'; Code='DE'; Length=4574; Lat=49.3278; Lon=8.5656; Radius=1800 }
@{ Id='sachsenring'; Name='Sachsenring'; City='Hohenstein-Ernstthal'; Country='Alemanha'; Code='DE'; Length=3670; Lat=50.7919; Lon=12.5733; Radius=1600 }
        @{ Id='oschersleben'; Name='Motorsport Arena Oschersleben'; City='Oschersleben'; Country='Alemanha'; Code='DE'; Length=3667; Lat=52.0267; Lon=11.2833; Radius=1600 }
        @{ Id='brands-hatch'; Name='Brands Hatch'; City='West Kingsdown'; Country='Reino Unido'; Code='GB'; Length=3908; Lat=51.3569; Lon=0.2628; Radius=1500 }
        @{ Id='silverstone'; Name='Silverstone Circuit'; City='Silverstone'; Country='Reino Unido'; Code='GB'; Length=5891; Lat=52.0733; Lon=-1.0147; Radius=2000 }
        @{ Id='donington'; Name='Donington Park'; City='Castle Donington'; Country='Reino Unido'; Code='GB'; Length=4020; Lat=52.8300; Lon=-1.3767; Radius=1600 }
        @{ Id='oulton-park'; Name='Oulton Park'; City='Little Budworth'; Country='Reino Unido'; Code='GB'; Length=4347; Lat=53.1833; Lon=-2.6167; Radius=1600 }
        @{ Id='barcelona'; Name='Circuit de Barcelona-Catalunya'; City='Montmeló'; Country='Espanha'; Code='ES'; Length=4657; Lat=41.5700; Lon=2.2611; Radius=1600 }
        @{ Id='jerez'; Name='Circuito de Jerez'; City='Jerez de la Frontera'; Country='Espanha'; Code='ES'; Length=4428; Lat=36.7083; Lon=-6.0333; Radius=1600 }
        @{ Id='valencia'; Name='Circuit Ricardo Tormo'; City='Cheste'; Country='Espanha'; Code='ES'; Length=4005; Lat=39.4856; Lon=-0.6272; Radius=1500 }
        @{ Id='aragon'; Name='MotorLand Aragón'; City='Alcañiz'; Country='Espanha'; Code='ES'; Length=5445; Lat=41.0783; Lon=-0.2036; Radius=1800 }
        @{ Id='navarra'; Name='Circuito de Navarra'; City='Los Arcos'; Country='Espanha'; Code='ES'; Length=3933; Lat=42.2442; Lon=-1.9064; Radius=1600 }
        @{ Id='portimao'; Name='Autódromo Internacional do Algarve'; City='Portimão'; Country='Portugal'; Code='PT'; Length=4653; Lat=37.2333; Lon=-8.6333; Radius=1800 }
        @{ Id='estoril'; Name='Circuito do Estoril'; City='Estoril'; Country='Portugal'; Code='PT'; Length=4182; Lat=38.7500; Lon=-9.3833; Radius=1600 }
        @{ Id='paul-ricard'; Name='Circuit Paul Ricard'; City='Le Castellet'; Country='França'; Code='FR'; Length=5842; Lat=43.2506; Lon=5.7917; Radius=1800 }
        @{ Id='magny-cours'; Name='Circuit de Nevers Magny-Cours'; City='Magny-Cours'; Country='França'; Code='FR'; Length=4411; Lat=46.8642; Lon=3.1639; Radius=1800 }
        @{ Id='dijon'; Name='Circuit de Dijon-Prenois'; City='Prenois'; Country='França'; Code='FR'; Length=3801; Lat=47.3667; Lon=4.9000; Radius=1600 }
        @{ Id='red-bull-ring'; Name='Red Bull Ring'; City='Spielberg'; Country='Áustria'; Code='AT'; Length=4318; Lat=47.2197; Lon=14.7647; Radius=1600 }
        @{ Id='hungaroring'; Name='Hungaroring'; City='Mogyoród'; Country='Hungria'; Code='HU'; Length=4381; Lat=47.5789; Lon=19.2486; Radius=1600 }
        @{ Id='brno'; Name='Automotodrom Brno'; City='Brno'; Country='Tchéquia'; Code='CZ'; Length=5403; Lat=49.2036; Lon=16.5594; Radius=1200 }
        @{ Id='most'; Name='Autodrom Most'; City='Most'; Country='Tchéquia'; Code='CZ'; Length=4212; Lat=50.5250; Lon=13.6361; Radius=1600 }
        @{ Id='slovakiaring'; Name='Slovakia Ring'; City='Orechová Potôň'; Country='Eslováquia'; Code='SK'; Length=5922; Lat=48.0531; Lon=17.7369; Radius=1800 }
        @{ Id='watkins-glen'; Name='Watkins Glen International'; City='Watkins Glen'; Country='Estados Unidos'; Code='US'; Length=5552; Lat=42.3369; Lon=-76.9269; Radius=1800 }
        @{ Id='road-atlanta'; Name='Road Atlanta'; City='Braselton'; Country='Estados Unidos'; Code='US'; Length=4088; Lat=34.1500; Lon=-83.8167; Radius=1800 }
        @{ Id='road-america'; Name='Road America'; City='Elkhart Lake'; Country='Estados Unidos'; Code='US'; Length=6515; Lat=43.7981; Lon=-87.9917; Radius=2200 }
        @{ Id='laguna-seca'; Name='WeatherTech Raceway Laguna Seca'; City='Monterey'; Country='Estados Unidos'; Code='US'; Length=3602; Lat=36.5844; Lon=-121.7528; Radius=1600 }
        @{ Id='sebring'; Name='Sebring International Raceway'; City='Sebring'; Country='Estados Unidos'; Code='US'; Length=6019; Lat=27.4544; Lon=-81.3478; Radius=1800 }
        @{ Id='cota'; Name='Circuit of the Americas'; City='Austin'; Country='Estados Unidos'; Code='US'; Length=5513; Lat=30.1328; Lon=-97.6411; Radius=1800 }
        @{ Id='mid-ohio'; Name='Mid-Ohio Sports Car Course'; City='Lexington'; Country='Estados Unidos'; Code='US'; Length=3614; Lat=40.6892; Lon=-82.6367; Radius=1600 }
        @{ Id='sonoma'; Name='Sonoma Raceway'; City='Sonoma'; Country='Estados Unidos'; Code='US'; Length=4266; Lat=38.1606; Lon=-122.4500; Radius=1600 }
        @{ Id='mosport'; Name='Canadian Tire Motorsport Park'; City='Bowmanville'; Country='Canadá'; Code='CA'; Length=3940; Lat=44.0500; Lon=-78.6667; Radius=1600 }
        @{ Id='suzuka'; Name='Suzuka International Racing Course'; City='Suzuka'; Country='Japão'; Code='JP'; Length=5807; Lat=34.8431; Lon=136.5406; Radius=1800 }
        @{ Id='fuji'; Name='Fuji Speedway'; City='Oyama'; Country='Japão'; Code='JP'; Length=4563; Lat=35.3717; Lon=138.9269; Radius=1800 }
        @{ Id='motegi'; Name='Twin Ring Motegi'; City='Motegi'; Country='Japão'; Code='JP'; Length=4801; Lat=36.5333; Lon=140.2167; Radius=1800 }
        @{ Id='okayama'; Name='Okayama International Circuit'; City='Mimasaka'; Country='Japão'; Code='JP'; Length=3703; Lat=34.9150; Lon=134.2200; Radius=1600 }
        @{ Id='sepang'; Name='Sepang International Circuit'; City='Sepang'; Country='Malásia'; Code='MY'; Length=5543; Lat=2.7608; Lon=101.7375; Radius=1800 }
        @{ Id='buriram'; Name='Chang International Circuit'; City='Buriram'; Country='Tailândia'; Code='TH'; Length=4554; Lat=14.9564; Lon=103.0847; Radius=1800 }
        @{ Id='mount-panorama'; Name='Mount Panorama'; City='Bathurst'; Country='Austrália'; Code='AU'; Length=6213; Lat=-33.4467; Lon=149.5600; Radius=2200 }
        @{ Id='phillip-island'; Name='Phillip Island Grand Prix Circuit'; City='Phillip Island'; Country='Austrália'; Code='AU'; Length=4445; Lat=-38.5000; Lon=145.2333; Radius=1800 }
        @{ Id='bahrain'; Name='Bahrain International Circuit'; City='Sakhir'; Country='Bahrein'; Code='BH'; Length=5412; Lat=26.0325; Lon=50.5106; Radius=1800 }
        @{ Id='yas-marina'; Name='Yas Marina Circuit'; City='Abu Dhabi'; Country='Emirados Árabes Unidos'; Code='AE'; Length=5554; Lat=24.4672; Lon=54.6031; Radius=1800 }
        @{ Id='losail'; Name='Lusail International Circuit'; City='Lusail'; Country='Catar'; Code='QA'; Length=5380; Lat=25.4833; Lon=51.4500; Radius=1800 }
    )
}
