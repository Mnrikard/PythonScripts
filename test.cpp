#include <iostream>
using namespace std;


class Deck
{
	public:
	
};

class Card
{
	public:
	string Suit;
	string Denomination;

	string GetCardName()
	{
		return this->Denomination+" of "+this->Suit;
	}
};

int main()
{
	Card* c = new Card();
	c->Suit = "Clubs";
	c->Denomination = "Ace";

	cout << c->GetCardName();
	return 0;
}
