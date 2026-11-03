# GDD Inicial - FUN BAR GAME

> **Nome:** FUN BAR GAME
> **Data:** 03/11/2026  
> **Nota:** Documento feito para calibrar as ideias de equipa.

## 1. Ideia geral

Jogo **3D multiplayer cooperativo** em que os jogadores trabalham juntos para gerir um bar. Recebem pedidos, preparam bebidas, servem clientes e manteêm o bar a funcionar.

O objetivo é criar uma experiência divertidade (podendo entrar no caótico) de gestão de bar, com especial atenção à **preparação das bebidas**. Queremos que esta seja uma das partes mais interessantes do gameplay, e não apenas carregar num botão e receber uma bebida pronta.

O jogo poderá ter momentos mais agitados quando aparecerem vários pedidos ou situações inesperadas. Ritmo e nível de dificuldade por decidir.

## 2. Core loop

1. Receber o pedido de um cliente.
2. Escolher o copo e os ingredientes necessários.
3. Preparar a bebida através de interações com os objetos e equipamentos.
4. Entregar a bebida ao cliente.
5. Passar ao pedido seguinte, gerindo as tarefas do bar em equipa.

**Proposta para a preparação:** trabalhar com receitas que envolvem vários passos (por exemplo, escolher ingredientes, servir quantidades e misturar). As ações e regras exatas ainda precisam de ser desenhadas e testadas. A cerveja poderá ter uma interação própria.

## 3. Sistemas previstos

### Base do jogo - desenvolver primeiro

- **Ações do jogador:** movimento, interação com objetos normais e do bar.
- **Pedidos dos clientes:** sistema para receber e consultar pedidos.
- **Receitas:** preparar diferentes bebidas através de ingredientes.
- **Copos:** usar tipos de copos diferentes, quando fizer sentido para a receita.
- **Serviço:** entregar bebidas e verificar se correspondem ao pedido.
- **Clientes:** comportamento básico para pedir, esperar e receber bebidas.
- **Multiplayer cooperativo:** permitir que os jogadores partilhem tarefas.

### Sistemas a acrescentar depois da base

- **Cerveja:** mecânica própria para tirar e servir cerveja.
- **Aperitivos:** decidir se entram no jogo e como funcionam.
- **Tarefas:** tarefas a gerir entre os players, desde preparação de bebidas, a limpeza do bar e gestão de stock.
- **Lavagem de loiça:** decidir entre máquina ou lavagem manual.
- **Upgrades:** ideias como TV e jukebox, com funcionamento a definir.
- **Gestão adicional:** stock e paciência dos clientes.

### Se houver tempo

- **Voice chat**
- **Identidade:** Mais decorações e opções de personalização.
- **Eventos especiais:** casa de banho entúpida, cliente bêbado e dia de jogo de futebol.

> Esta ordem é uma **proposta de desenvolvimento**.

## 4. Primeira versão jogável (MVP)

A primeira versão deve permitir testar o ciclo completo **pedido -> preparação -> entrega**.

Para isso:

- Um bar pequeno e simples.
- Movimento e interação com objetos.
- Um ou dois clientes com pedidos básicos.
- Uma ou duas receitas com preparação por passos.
- Copos e ingredientes necessários para essas receitas.
- Entrega do pedido e resposta simples de sucesso/erro.

TBD:

- Multiplayer funcional para testar a cooperação.

## 5. Arte e apresentação

A direção visual **não está decidida**. A ideia mais provável é um jogo **3D estilizado, próximo de low/mid poly**, mas ainda não existem escolhas concretas sobre estilo, personagens, ambiente ou interface.

Para o protótipo, podem ser usados modelos simples e temporários.

## 6. Áudio e QA

**Áudio:** começar por sons das interações principais (copos, ingredientes, equipamentos e entrega), feedback dos pedidos e ambiente do bar. A jukebox poderá vir a ter música, se for implementada.

**QA:** testar principalmente se os passos das receitas funcionam, se os pedidos podem ser concluídos e se os jogadores conseguem partilhar objetos sem problemas no multiplayer.

## 7. Ideias e decisões para discutir com a equipa

- Como funciona exatamente a preparação de cocktails? Que ações são divertidas sem se tornarem repetitivas?
- Que receitas e equipamentos usamos primeiro no protótipo?
- Quantos jogadores queremos suportar?
- Como funciona o dia de trabalho: tempo, dinheiro ou outro objetivo?
- Como funcionam os clientes e que consequências existem para erros e demoras?
- Os aperitivos e a lavagem de loiça valem o tempo de desenvolvimento?
- Que upgrades e eventos acrescentam realmente algo ao gameplay?
- Que direção artística queremos seguir?

---

**Resumo:** a nossa proposta é começar por um pequeno jogo cooperativo de gestão de bar em que a preparação de bebidas é o centro da experiência. Primeiro fazemos o ciclo principal funcionar; depois vamos acrescentando os restantes sistemas conforme o tempo e os testes.
