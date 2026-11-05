# GDD - BARKEEP

> **Nome:** BARKEEP
> **Data:** 05/11/2026
> **Plataforma:** PC
> **Engine:** Unity

## 1. Ideia geral

Jogo **3D multiplayer cooperativo** (1-4 players) em que os jogadores trabalham juntos para gerir um bar. Recebem pedidos, preparam bebidas, servem clientes e manteêm o bar a funcionar.

O objetivo é criar uma experiência divertida e cooperativa de gestão de bar, com especial atenção à **preparação de bebidas**. Queremos que esta seja uma das partes mais interessantes do gameplay, e que não seja apenas carregar num botão e receber uma bebida pronta.

O jogo poderá ter momentos mais agitados e caóticos quando aparecerem vários pedidos ou situações inesperadas.

**Pilares do jogo**

- Preparação: bebidas feitas através de interações com ingredientes e equipamentos.
- Cooperação: jogadores dividem tarefas e trabalham no mesmo espaço.
- Serviço: equilibrar qualidade das bebidas, tempo e satisfação dos clientes.
- Gestão: limpeza e controlo do stock.

## 2. Estrutura e ciclos de jogo

### 2.1. Core Gameplay Loop

1. Receber o pedido.
2. Preparar o pedido.
3. Entregar a bebida.
4. Gerir o bar.

### 2.2. Ciclo do Jogo

1. Criar/carregar bar e sessão: o host abre o bar e recebe os jogadores.
2. Preparação: verificar avisos, gerir stock, organizar estações e consultar upgrades.
3. Início do serviço: jogadores confirmam que estão prontos.
4. Serviço: receber clientes, preparar pedidos e responder a situações inesperadas.
5. Encerramento: terminar pedidos pendentes e fechar o atendimento.
6. Resultados: consultar receitas, despesas, satisfação e problemas do dia.
7. Continuidade: avançar para outro dia.

### 2.3. Dias, semanas e eventos

A progressão será organizada por dias e semanas, com maior pressão à medida que o jogo avança. Existe a ideia de preparar **um dia especial por semana** (_Boss Day_), por exemplo, um dia de futebol.

## 3. Jogador, objetos e estações

### 3.1. Ações e física

- Movimento livre em First-Person dentro do bar.
- Agarrar, transportar, pousar e lançar objetos físicos.
- Transporte de um objeto de cada vez; tabuleiros poderão transportar vários itens.
- Colisões poderão derrubar objetos, provocar derrames e partir objetos frágeis.
- Quando um tabuleiro cai, os itens que transporta também poderão cair.

> O comportamento da física, as penalizações e a sincronização dos objetos têm de ser testados em multiplayer.

### 3.2. Estações e minijogos

Certas tarefas poderão usar uma câmara de interação própria e controlos sobretudo pelo rato. A proposta inicial prevê uma pessoa por estação de cada vez; durante a interação, o jogador fica imóvel, mas continua sujeito a colisões que podem interromper a tarefa.

**Estações:** preparar ingredientes, misturar bebidas com shaker, tirar cerveja, lavar loiça.

## 4. Bebidas, receitas e qualidade

### 4.1. Preparação de cocktails

A regra-base discutida é: **juntar bebidas + ingredientes adicionais + misturar**.

**Propostas a validar no protótipo:**

- Quantidades servidas livremente, com velocidade de despejo dependente da inclinação da garrafa.
- Líquidos representados visualmente, sem simulação física completa de fluidos.
- Informação sobre ingredientes e quantidades no recipiente.
- Receitas configuráveis no Inspector do Unity.

**Ainda por definir:** copos específicos, quantidades corretas e tolerâncias na avaliação.

### 4.2. Bebidas escolhidas para o protótipo

| Bebida           | Ingredientes/ preparação                                             |
| ---------------- | -------------------------------------------------------------------- |
| **Haiti Libre**  | Whisky, Soda, Ice, Garnish                                           |
| **Short Island** | Vodka, Rum, Gin, Tequila, Triple Sec, Lime Juice, Soda, Ice, Garnish |
| **Atomic Gin**   | Gin, Tonic, Ice, Garnish                                             |
| **Beer**         | Beer Keg                                                             |

_O **Long Island pode ser simplificado** para o protótipo._

### 4.3. Avaliação das bebidas

A qualidade poderá depender dos **ingredientes, quantidades e tolerância do cliente**. Os mesmos não terão todos a mesma exigência: alguns aceitam pequenas diferenças e outros exigem maior precisão.

A **cerveja** terá uma interação própria. A proposta prevê que a quantidade de espuma influencie a qualidade.

## 5. Clientes, pedidos e satisfação

- Pedidos: clientes pedem bebidas através de informação visível, inicialmente proposta em balões por cima das personagens.
- Localização: podem dirigir-se ao balcão ou às mesas; uma esplanada foi sugerida, mas a sua presença no primeiro cenário não está fechada.
- Entrega: proposta de clicar no cliente enquanto se segura a bebida. Com tabuleiro, poderá ser selecionada automaticamente a bebida correspondente.
- Paciência: diminui depois de o pedido ser feito, incluindo durante tentativas de correção.
- Exigência: diferentes clientes aceitam diferentes margens de erro.
- Segunda oportunidade: uma bebida rejeitada pode ser corrigida/refeita **uma vez**; se continuar incorreta, o cliente sai.
- Outros comportamentos propostos: pedidos repetidos com intervalos, personalidades, embriaguez, saída condicionada pela satisfação e limitação de lugares disponíveis.

Bebidas rejeitadas poderão ser corrigidas, reaproveitadas ou deitadas fora, dependendo das regras que vierem a ser acordadas.

**Foco dos testes de balanceamento:** tempo de espera, margem de erro, legibilidade dos pedidos e consequências da insatisfação.

## 6. Gestão do bar e economia

### 6.1. Stock e entregas

O stock é limitado e a gestão acontece sobretudo _entre dias_. A proposta prevê compras antes do serviço sem reposição automática durante o atendimento.

As entregas surgem em caixas durante a preparação do dia; os jogadores transportam-nas para locais de armazenamento, onde o conteúdo fica disponível. Os ingredientes terão locais próprios e os recipientes vazios permanecerão no mundo até serem deitados fora. **Ingredientes não expiram com o tempo.**

### 6.2. Staff room e interface de gestão

Manter o espaço público do bar visualmente _clean_, sem um grande sistema de gestão à vista. Na **staff room** existirá um tablet ou laptop para consultar avisos e gerir o stock.

Compras, upgrades, preços e outras funções poderão ser associados a esse dispositivo, mas ainda não estão todos confirmados. O objetivo é concentrar a gestão num local reconhecível sem encher o balcão de interfaces.

### 6.3. Economia e resultados

A equipa partilha o mesmo saldo. O sistema de gestão inclui receitas de vendas, compras de stock, preços ajustáveis, despesas (renda, água e eletricidade; proposta de pagamento semana a semana) e estatísticas no final de cada dia.

## 7. Progressão, upgrades e eventos

A dificuldade poderá aumentar através do volume de clientes, exigência e situações inesperadas.

**Upgrades sugeridos:** equipamentos, TV, jukebox e possíveis melhorias de serviço.

**Eventos e problemas sugeridos:** cliente bêbado, casa de banho entupida, dia de futebol e chegada de turistas. Os eventos poderão afetar ritmo e satisfação.

**Conteúdo opcional:** decoração adicional e voice chat.

## 8. Multiplayer e Save System

### 8.1. Multiplayer

O jogo terá cooperação online para 1–4 jogadores.

**Proposta de implementação:**

- Modelo host-client, com o host responsável pela validação e estado partilhado.
- Entrada por código de sala; convites Steam como possibilidade adicional.
- Entrada de novos jogadores durante a preparação do dia, não durante o serviço.
- Saída do host encerra a sessão para os restantes jogadores.
- Menus pessoais não pausam o serviço.

A tecnologia de networking, relay, descoberta de sessões e integração Steam ainda têm de ser escolhidas pela equipa de programação.

### 8.2. Saves e recuperação

A proposta é de vários bares associados ao host, com **save no início de cada dia**, e persistência de dados como saldo, stock, upgrades e posição de objetos relevantes.

É essencial distinguir:

- **Interrupção técnica:** recupera o estado guardado antes do serviço.
- **Derrota no gameplay:** entre perder o bar definitivamente ou repetir o dia. **TBD**

Estas duas situações não devem ser tratadas como equivalentes.

## 9. Arte, interface e espaço

Direção visual atual: 3D estilizado com distribuição **low/mid-poly** conforme a importância dos elementos.

- **Mid-poly:** NPCs e objetos interativos, para facilitar a identificação e a leitura das ações.
- **Low-poly:** elementos secundários e cenários fora do foco principal do jogador.

O espaço deverá permitir circular entre balcão, preparação, armazenamento e staff room sem dificultar a cooperação. Pedidos, ingredientes, objetos utilizáveis e feedback das ações precisam de ser claros.

## 10. Áudio

O áudio deve ajudar a perceber o que acontece no bar e reforçar o ambiente, com **som espacial 3D** como direção de implementação.

**Necessidades iniciais:** sons de copos, garrafas, ingredientes, equipamentos, mistura, cerveja e feedback de ações. Os clientes poderão comunicar através de **gibberish**, genérico.

A lista final de assets, implementação e regras de mistura áudio serão definidas conforme as mecânicas forem estabilizadas.

## 11. QA e playtesting

O QA acompanha o desenvolvimento desde os primeiros protótipos:

- **Receitas:** ingredientes, quantidades, mistura, qualidade, correções e entrega.
- **Multiplayer:** interação simultânea, autoridade do host, sincronização de objetos e entradas/saídas de jogadores.
- **Física:** agarrar, pousar, lançar, colisões, tabuleiros e interrupções de minijogos.
- **Clientes:** pedidos, paciência, tolerância.
- **Gestão e saves:** stock, preparação, resultados e recuperação após interrupção.
- **Usabilidade e performance:** clareza do feedback, cooperação entre jogadores e estabilidade com mais clientes.

## 12. Prioridades de desenvolvimento

Esta ordem é **indicativa**, não é um calendário nem uma lista de funcionalidades já implementadas. Arte, áudio e QA podem avançar em paralelo.

| Etapa                            | Objetivo                                                                          | Resultado esperado                                        |
| -------------------------------- | --------------------------------------------------------------------------------- | --------------------------------------------------------- |
| **1. Fundação Multiplayer**      | Sessão host-client, movimentação e interações físicas básicas.                    | Vários jogadores no mesmo espaço a interagir com objetos. |
| **2. Vertical slice / MVP base** | Um cliente, um pedido, uma bebida, entrega, avaliação e feedback, em multiplayer. | Ciclo completo jogável                                    |
| **3. Expansão do protótipo**     | Menu de bebidas; Equipamentos, ingredientes e minijogos essenciais                | Sistema de bebidas testável com variedade                 |
| **4. Gestão e dias**             | Preparação, stock, resultados, economia e persistência                            | Vários dias ligados por um ciclo funcional                |
| **5. Sistemas adicionais**       | Lavagem, tabuleiros, upgrades e eventos                                           | Maior variedade, se o tempo permitir                      |
| **6. Conteúdo e polimento**      | Arte, UI, áudio, balanceamento, testes e otimização                               | Versão estável                                            |

**Critério mínimo para o primeiro MVP:** cooperação online funcional e pelo menos um pedido preparado, entregue e avaliado do início ao fim.

## 13. Orientações técnicas

Proposta de desenvolvimento **data-driven e modular**, com receitas e conteúdos configuráveis no Inspector do `Unity`, separação entre definições e instâncias, validação pelo host, comunicação por eventos quando útil.

Orientação facilita novas receitas e sistemas sem refazer a base do jogo.

## 14. Decisões pendentes para revisão

**Prioridade alta - afetam sistemas centrais:**

- Consequência da derrota: nova campanha ou repetição do dia.
- Validação do ciclo diário (incluindo duração de 20 min + 30 s e votação do início).
- Quantidades, copos, passos de mistura e critérios de avaliação das receitas.
- Regras de satisfação, pagamentos e custos indispensáveis ao primeiro ciclo de gestão.

**Prioridade posterior - não bloqueiam a base:**

- Número de receitas finais.
- Lavagem de loiça e regras físicas mais complexas.
- Upgrades, eventos e organização exata dos Boss Days.
- Lotação, comportamento avançado dos NPCs e metas de performance.
- Layout final, identidade visual detalhada, HUD e funções adicionais do tablet.
- Jukebox, voice chat, decoração e outros conteúdos opcionais.

---

**Resumo de direção:** construir primeiro um bar cooperativo onde preparar e servir bebidas seja fiável em multiplayer. Expandir progressivamente para gestão por dias, eventos e personalização.
